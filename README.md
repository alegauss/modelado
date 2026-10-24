# Modelado

Modelado é um app para Windows que transforma **fotos de objetos em modelos 3D** com modelos de
IA rodando no próprio computador. É o irmão do [PhotoBoost](https://github.com/Japode/photo-boost):
a mesma arquitetura (modelos convertidos para ONNX por scripts Python, publicados no Hugging Face
com SHA-256 fixado, baixados pelo app na primeira vez e executados com ONNX Runtime na GPU ou na
CPU), mas em vez de melhorar a imagem, ele gera geometria. Nenhuma foto sai do PC.

| Operação | Modelos | Resultado |
| --- | --- | --- |
| **Objeto 3D** | U²-Net (recorte) → TripoSR (encoder + decoder) → marching cubes | Malha fechada, com frente, lados e costas, cores por vértice |
| **Relevo** | Depth Anything V2 Small (+ U²-Net opcional) | Baixo-relevo texturizado com a foto; com base sólida, pronto para imprimir |

O resultado aparece num visualizador 3D (arrastar gira, roda do mouse aproxima, duplo clique
centraliza; cores, argila ou arestas) e é exportado como **GLB** (glTF binário: Visualizador 3D
do Windows, Blender, web), **OBJ** (com .mtl e textura, ou cores por vértice), **STL** (impressão
3D, em milímetros) ou **PLY** (com cores: MeshLab, CloudCompare).

![Robô do exemplo do TripoSR reconstruído pelo Modelado](docs/robot.png)

## Requisitos

- **Windows 10 (1903+) ou 11, x64.**
- **GPU com DirectX 12** para rodar na placa de vídeo via DirectML (NVIDIA, AMD ou Intel). Sem
  ela, tudo roda na CPU, mais devagar; se um modelo falhar na GPU (por exemplo, sem memória de
  vídeo) ele é refeito na CPU e a barra de status diz isso.
- **Memória:** o encoder do TripoSR é o modelo pesado: 0,8 GB em FP16 (padrão na GPU) ou 1,7 GB
  em FP32. Conte com uns 4 GB livres de RAM ou de memória de vídeo para o Objeto 3D.
- **Disco:** 1,1 GB de modelos para o Objeto 3D em qualidade padrão (2 GB em alta), 50 a 100 MB
  para o Relevo.

## Usar

```
dotnet build Modelado.slnx
.\run.cmd
.\run.cmd --open foto.jpg --provider cpu
```

Requer o **.NET SDK 10** (`global.json`). Abra uma foto (ou arraste para a janela) e escolha
**Gerar objeto 3D** ou **Gerar relevo**. Na primeira vez, o app mostra quanto vai baixar, de onde
e sob qual licença; o download pode ser pausado e retomado, e cada arquivo é conferido por
SHA-256 antes de ser usado. **Sobre e modelos** lista o que está instalado, baixa tudo para uso
offline ou apaga o que foi baixado.

Dicas para um bom Objeto 3D: um único objeto, inteiro, centralizado, sobre um fundo simples, de
um ângulo levemente acima. Um PNG já recortado (com transparência) é usado como está. O
TripoSR inventa os lados que a foto não mostra: costas de objetos assimétricos saem aproximadas.

Opções da linha de comando do app: `--open <foto>`, `--models <pasta>`,
`--provider auto|dml|cpu`, `--quality standard|high`, `--manifest <url>`.

### Onde ficam os modelos

O app procura cada modelo em ordem:

1. a pasta `models` ao lado do executável;
2. `%LOCALAPPDATA%\Modelado\models`, para onde vão os downloads.

`--models <pasta>` substitui as duas. `run.cmd` usa `.cache\models` quando existe, que é onde
`prepare-models.cmd` grava. Nenhum modelo fica no repositório.

### Linha de comando

`tools\Modelado.Cli` roda os mesmos pipelines sem janela, para lotes e scripts (e em Linux ou
macOS, na CPU):

```
dotnet run --project tools\Modelado.Cli -c Release -- object cadeira.png -o cadeira.glb --preview vista.png
dotnet run --project tools\Modelado.Cli -c Release -- relief retrato.jpg -o retrato.stl --base 0.05 --size 100
dotnet run --project tools\Modelado.Cli -c Release -- download all
```

`object` aceita `--resolution 128..384`, `--threshold`, `--keep-background`, `--step`,
`--smooth`; `relief` aceita `--grid`, `--depth`, `--base`, `--remove-background`. Comuns:
`--provider`, `--high`, `--models`, `--manifest`, `--size` (maior lado ao salvar).

## Preparar os modelos (Windows, Python) e publicar no Hugging Face

Os modelos são baixados das fontes originais, convertidos para ONNX e conferidos contra o
PyTorch por scripts Python. Testado com **Python 3.13**; o PyTorch para CPU basta, sem CUDA. Um
comando faz tudo:

```
prepare-models.cmd
prepare-models.cmd --publish D:\Chaves\hf.txt
```

O que ele faz, passo a passo (cada um pode ser rodado sozinho):

1. **Ambiente:** cria `.venv` e instala `scripts\requirements.txt`.

   ```
   python -m venv .venv
   .venv\Scripts\python -m pip install torch==2.14.1 --index-url https://download.pytorch.org/whl/cpu
   .venv\Scripts\python -m pip install -r scripts\requirements.txt
   ```

2. **Baixar os pesos** (cerca de 1,9 GB, em `.cache\weights`): só biblioteca padrão. Cada
   arquivo está fixado num commit em `scripts\model-sources.json`, com tamanho e SHA-256; um
   download interrompido continua de onde parou e um arquivo já correto não é baixado de novo.

   ```
   .venv\Scripts\python scripts\download_models.py
   ```

3. **Converter para ONNX** (em `.cache\models`):

   ```
   .venv\Scripts\python scripts\export_models.py
   .venv\Scripts\python scripts\export_models.py --only depth-anything-v2-small
   ```

   Para cada modelo grava `fp32.onnx`, `fp16.onnx` quando ele se mantém, e um `meta.json` que o
   app lê. O TripoSR é dividido em dois: o **encoder** (foto 512 × 512 → triplano
   3 × 40 × 64 × 64) e o **decoder** (triplano + pontos → densidade e cor), e o app extrai a
   malha com marching cubes. Cada exportação roda no ONNX Runtime contra o PyTorch na imagem de
   teste do próprio modelo; uma que diverge além da tolerância falha o script em vez de gravar
   um modelo errado. Medido aqui:

   | Modelo | Variante | Comparação com o PyTorch |
   | --- | --- | --- |
   | triposr-encoder | FP32 | triplano com erro relativo 2·10⁻⁶; ocupação da grade idêntica |
   | triposr-encoder | FP16 | erro relativo 1·10⁻³; ocupação da grade idêntica |
   | triposr-decoder | FP32 | densidade 9·10⁻⁵ (relativo), cor 3·10⁻⁵ |
   | depth-anything-v2-small | FP32 / FP16 | 120 dB / 71,6 dB de PSNR na profundidade normalizada |
   | u2net | FP32 (export do rembg) | IoU 0,979 com o recorte original da cadeira |

4. **Publicar** em [alegauss/modelado-models](https://huggingface.co/alegauss/modelado-models)
   com um token de escrita do Hugging Face (huggingface.co/settings/tokens), lido de um arquivo
   ou de `HF_TOKEN` e nunca impresso:

   ```
   .venv\Scripts\python scripts\publish_models.py --dry-run
   .venv\Scripts\python scripts\publish_models.py --token-file D:\Chaves\hf.txt
   .venv\Scripts\python scripts\publish_models.py --token-file D:\Chaves\hf.txt --repo usuario/outro-repo
   ```

   São dois commits: primeiro os arquivos (`<modelo>-<variante>.onnx`, `<modelo>-meta.json`) e
   um model card com a licença de cada um; depois o `manifest.json`, com as URLs fixadas em
   `resolve/<commit 1>/`, para que nenhum upload posterior mude um arquivo que o app confere por
   SHA-256. O manifesto fixado é copiado para `src\Modelado.Inference\models-manifest.json`:
   recompile o app e ele passa a saber o tamanho e o hash de cada arquivo sem rede. Enquanto
   esse arquivo estiver vazio, o app lê o manifesto mais recente do repositório.

## Testes

```
run-tests.cmd
```

Compila a solução com avisos como erros e os analisadores de desempenho ligados, roda os
testes de cada projeto em `tests\` e os testes Python de `scripts\tests`. Os testes com os
modelos reais (a cadeira vira uma malha fechada, voltada para a frente; uma foto vira um relevo
sólido) rodam quando `.cache\models` existe e dizem que pularam quando não. Em Linux ou macOS,
`./run-tests.sh` faz o mesmo, exceto rodar o app WPF, que lá só é compilado.

## Como funciona

```
src/
  Modelado.Imaging    decodificar (com orientação EXIF), redimensionar, recortar, PNG
  Modelado.Geometry   malha, marching cubes, amostragem esparsa, relevo, limpeza,
                      exportação GLB/OBJ/PLY/STL, renderizador por software do visualizador
  Modelado.Inference  meta.json, pastas de modelos, downloads verificados, sessões ONNX Runtime
                      (DirectML → CPU), pipelines Objeto 3D e Relevo
  Modelado.App        o app WPF
tools/Modelado.Cli    os pipelines na linha de comando
scripts/              download, exportação ONNX, manifesto, publicação no Hugging Face
```

- **Objeto 3D:** recorte com U²-Net (o mesmo do `run.py` do TripoSR, via rembg), enquadramento
  em 85 % de um quadrado sobre cinza 0,5, encoder → triplano. A densidade do decoder é amostrada
  numa grade (256³ por padrão): primeiro a cada 2 pontos, depois na resolução completa só nas
  células que a superfície cruza e nas vizinhas. Nos testes com o TripoSR isso dá a **mesma
  malha, byte a byte**, que avaliar todos os 16,7 milhões de pontos, 5 vezes mais rápido. O
  marching cubes desenha a superfície no limiar 25 do TripoSR, pedaços soltos são removidos, a
  malha é suavizada (Taubin) e o decoder dá a cor de cada vértice.
- **Relevo:** a profundidade relativa do Depth Anything V2 (518 × 518) vira a altura de uma
  grade com o formato da foto, entre os percentis 2 e 98 para que um pixel solto não achate o
  resto. Com base, a superfície é fechada com fundo plano e paredes nas bordas.
- **Visualizador:** um rasterizador por software (z-buffer, Gouraud, faixas em paralelo):
  mesma imagem no app, na linha de comando e nos testes, sem depender de GPU para desenhar.

Tempos medidos na CPU (4 núcleos, sem GPU), com os modelos FP32:

| Operação | Etapas | Tempo |
| --- | --- | --: |
| Objeto 3D, grade 256 | recorte 2,4 s · encoder 30,6 s (com o carregamento de 1,7 GB) · densidade 4,4 s · superfície 0,6 s | 38 s |
| Relevo, grade 256 | profundidade 1,3 s · malha 0,2 s | 2,2 s |

## Licenças dos modelos

| Modelo | Licença |
| --- | --- |
| TripoSR (Stability AI e Tripo AI) | MIT |
| DINO ViT-B/16 (configuração do encoder do TripoSR) | Apache-2.0 |
| Depth Anything V2 **Small** | Apache-2.0 (Base e Large são CC BY-NC 4.0 e por isso não são usados) |
| U²-Net | Apache-2.0 (pesos), MIT (export ONNX do rembg) |

Todos permitem redistribuição e uso comercial, por isso todos são hospedados no repositório
de modelos do Hugging Face. O código do TripoSR usado na exportação está em
`scripts\archs\triposr` (MIT, com a licença original). O painel Sobre do app lista cada modelo
instalado com o link da licença.
