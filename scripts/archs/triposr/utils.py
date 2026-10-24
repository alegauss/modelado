# Vendored from TripoSR (github.com/VAST-AI-Research/TripoSR, commit 107cefdc, MIT; LICENSE beside
# this file). Only what the models import is kept: the image preprocessing, cameras, mesh and
# video helpers are dropped, with their rembg, trimesh and imageio imports, since Modelado does
# those steps itself (export_models.py and the C# pipelines).
from collections import defaultdict
from dataclasses import dataclass
from typing import Any, Callable, Dict, Optional, Tuple, Union

import torch
import torch.nn as nn
import torch.nn.functional as F
from omegaconf import DictConfig, OmegaConf


def parse_structured(fields: Any, cfg: Optional[Union[dict, DictConfig]] = None) -> Any:
    scfg = OmegaConf.merge(OmegaConf.structured(fields), cfg)
    return scfg


class BaseModule(nn.Module):
    @dataclass
    class Config:
        pass

    cfg: Config  # add this to every subclass of BaseModule to enable static type checking

    def __init__(
        self, cfg: Optional[Union[dict, DictConfig]] = None, *args, **kwargs
    ) -> None:
        super().__init__()
        self.cfg = parse_structured(self.Config, cfg)
        self.configure(*args, **kwargs)

    def configure(self, *args, **kwargs) -> None:
        raise NotImplementedError


def rays_intersect_bbox(
    rays_o: torch.Tensor,
    rays_d: torch.Tensor,
    radius: float,
    near: float = 0.0,
    valid_thresh: float = 0.01,
):
    input_shape = rays_o.shape[:-1]
    rays_o, rays_d = rays_o.view(-1, 3), rays_d.view(-1, 3)
    rays_d_valid = torch.where(
        rays_d.abs() < 1e-6, torch.full_like(rays_d, 1e-6), rays_d
    )
    if type(radius) in [int, float]:
        radius = torch.FloatTensor(
            [[-radius, radius], [-radius, radius], [-radius, radius]]
        ).to(rays_o.device)
    radius = (
        1.0 - 1.0e-3
    ) * radius  # tighten the radius to make sure the intersection point lies in the bounding box
    interx0 = (radius[..., 1] - rays_o) / rays_d_valid
    interx1 = (radius[..., 0] - rays_o) / rays_d_valid
    t_near = torch.minimum(interx0, interx1).amax(dim=-1).clamp_min(near)
    t_far = torch.maximum(interx0, interx1).amin(dim=-1)

    # check wheter a ray intersects the bbox or not
    rays_valid = t_far - t_near > valid_thresh

    t_near[torch.where(~rays_valid)] = 0.0
    t_far[torch.where(~rays_valid)] = 0.0

    t_near = t_near.view(*input_shape, 1)
    t_far = t_far.view(*input_shape, 1)
    rays_valid = rays_valid.view(*input_shape)

    return t_near, t_far, rays_valid


def chunk_batch(func: Callable, chunk_size: int, *args, **kwargs) -> Any:
    if chunk_size <= 0:
        return func(*args, **kwargs)
    B = None
    for arg in list(args) + list(kwargs.values()):
        if isinstance(arg, torch.Tensor):
            B = arg.shape[0]
            break
    assert (
        B is not None
    ), "No tensor found in args or kwargs, cannot determine batch size."
    out = defaultdict(list)
    out_type = None
    # max(1, B) to support B == 0
    for i in range(0, max(1, B), chunk_size):
        out_chunk = func(
            *[
                arg[i : i + chunk_size] if isinstance(arg, torch.Tensor) else arg
                for arg in args
            ],
            **{
                k: arg[i : i + chunk_size] if isinstance(arg, torch.Tensor) else arg
                for k, arg in kwargs.items()
            },
        )
        if out_chunk is None:
            continue
        out_type = type(out_chunk)
        if isinstance(out_chunk, torch.Tensor):
            out_chunk = {0: out_chunk}
        elif isinstance(out_chunk, tuple) or isinstance(out_chunk, list):
            chunk_length = len(out_chunk)
            out_chunk = {i: chunk for i, chunk in enumerate(out_chunk)}
        elif isinstance(out_chunk, dict):
            pass
        else:
            print(
                f"Return value of func must be in type [torch.Tensor, list, tuple, dict], get {type(out_chunk)}."
            )
            exit(1)
        for k, v in out_chunk.items():
            v = v if torch.is_grad_enabled() else v.detach()
            out[k].append(v)

    if out_type is None:
        return None

    out_merged: Dict[Any, Optional[torch.Tensor]] = {}
    for k, v in out.items():
        if all([vv is None for vv in v]):
            # allow None in return value
            out_merged[k] = None
        elif all([isinstance(vv, torch.Tensor) for vv in v]):
            out_merged[k] = torch.cat(v, dim=0)
        else:
            raise TypeError(
                f"Unsupported types in return value of func: {[type(vv) for vv in v if not isinstance(vv, torch.Tensor)]}"
            )

    if out_type is torch.Tensor:
        return out_merged[0]
    elif out_type in [tuple, list]:
        return out_type([out_merged[i] for i in range(chunk_length)])
    elif out_type is dict:
        return out_merged


ValidScale = Union[Tuple[float, float], torch.FloatTensor]


def scale_tensor(dat: torch.FloatTensor, inp_scale: ValidScale, tgt_scale: ValidScale):
    if inp_scale is None:
        inp_scale = (0, 1)
    if tgt_scale is None:
        tgt_scale = (0, 1)
    if isinstance(tgt_scale, torch.FloatTensor):
        assert dat.shape[-1] == tgt_scale.shape[-1]
    dat = (dat - inp_scale[0]) / (inp_scale[1] - inp_scale[0])
    dat = dat * (tgt_scale[1] - tgt_scale[0]) + tgt_scale[0]
    return dat


def get_activation(name) -> Callable:
    if name is None:
        return lambda x: x
    name = name.lower()
    if name == "none":
        return lambda x: x
    elif name == "exp":
        return lambda x: torch.exp(x)
    elif name == "sigmoid":
        return lambda x: torch.sigmoid(x)
    elif name == "tanh":
        return lambda x: torch.tanh(x)
    elif name == "softplus":
        return lambda x: F.softplus(x)
    else:
        try:
            return getattr(F, name)
        except AttributeError:
            raise ValueError(f"Unknown activation function: {name}")
