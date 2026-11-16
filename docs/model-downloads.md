# Downloading gated models

What the MOD122 spike found on 16/11/2026 about models that sit behind a licence form on
Hugging Face, here the Stability AI pair SF3D (`stabilityai/stable-fast-3d`) and SPAR3D
(`stabilityai/stable-point-aware-3d`). These two were accepted for Modelado on 12/11/2026.
Today the downloader (`ModelDownloads`) fetches public files only, by pinned commit and SHA-256.

## What Hugging Face answers

Both repositories are `gated: auto`: the licence form approves an account as soon as it is
submitted, with no human review. The repository's metadata, README and LICENSE.md stay public.
Every other file is gated:

| Request for `config.yaml` | Answer |
| --- | --- |
| No token | `401`: not signed in |
| A valid token whose account has not agreed | `403`: "you are not in the authorized list. Visit https://huggingface.co/stabilityai/stable-fast-3d to ask for access." |
| A valid token whose account has agreed | `200`: the file |

So a client can tell the two things a person has to fix apart without guessing: 401 means
sign in (or paste a token), and 403 means open the model's page and agree. There is no API to
agree on someone's behalf, and there should not be one.

## The form

The form is part of the model card (`extra_gated_fields`). It asks for:

- name, email, country and organisation;
- whether to receive Stability's promotions;
- the intended use: Research, Personal use, Creative Professional, Startup or Enterprise;
- a checkbox agreeing to the Stability AI Community License and Stability's privacy policy.

Stability also asks anyone using the models for a commercial purpose to register at
https://stability.ai/community-license, outside Hugging Face. The licence ends for anyone
whose organisation makes more than US $1M a year.

## What the licence lets Modelado do

The Stability AI Community License (LICENSE.md in each repository) allows using,
reproducing and distributing the models and Derivative Works, which includes an ONNX export.
Whoever distributes them must:

1. ship a copy of the Agreement with them;
2. ship a Notice file that holds "This Stability AI Model is licensed under the Stability AI
   Community License, Copyright © Stability AI Ltd. All Rights Reserved", and says that the
   model was changed and how (here: exported to ONNX, and quantised where it is);
3. prominently display "Powered by Stability AI" in the app, its About or its documentation.

If the distribution is commercial, the distributor also registers with Stability.

Someone who receives the model inside an integrated end-user product is outside section III,
the commercial terms. A Modelado user therefore needs no Hugging Face account, no token and no
Stability registration when the ONNX comes from Modelado's own models repository.

## The flow this points to

1. **Publishing.** Modelado's maintainer agrees to the form once, on the account that
   publishes the models. The maintainer registers with Stability if Modelado's distribution
   counts as commercial; that is the maintainer's call. The weights are fetched with that
   account's token, exported to ONNX, and published to the models repository. The Agreement
   and the Notice file go beside them. Licence-class handling is MOD125.
2. **In the app.** The download's consent panel shows the licence and asks for consent before
   the download (MOD123, built): a manifest entry with `conditions` (summary, attribution and
   registration URL, carried from the model's meta.json `upstream.conditions` by
   build_manifest.py) adds them in plain words, with the full text and an "I agree" box that
   Download waits on. The agreement is kept in settings.json under `modelConsents`, by model
   and licence URL, so it is asked once per licence text. A permissive model still downloads in
   one click. The attribution (MOD126) is shown wherever the model's result appears. The
   download itself is an ordinary public fetch, so the user meets no login and no
   registration.
3. **Fallback, only if a licence forbids redistribution.** The user pastes a Hugging Face token,
   stored with DPAPI as the cloud keys are. The app probes one file and acts on the answer:
   - **401:** ask for a token again.
   - **403:** open the model's page in the browser so the user can agree, then probe again.
   - **200:** fetch the files and export on the user's PC.

   Neither Stability model needs this path.

## The prototype

A throwaway script fetched files with a token read from a file and never printed. Run against
SF3D with a valid token on an account that has not agreed:

- the gated `config.yaml` came back 403, with the message above;
- the public `LICENSE.md` came back 200.

The 200 path for a gated file has not been run: it needs the form submitted on the
account, with the person's own details, which only they can do.
