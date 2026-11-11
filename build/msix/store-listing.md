# Microsoft Store listing — Modelado (MOD79)

What Partner Center asks for when the package is submitted, kept beside the manifest so a
submission and the code it describes are reviewed together. Keep the wording in step with the
README and the site: the pitch and the privacy facts are the same claims.

**The language sections below are the source; the listing CSV and the screenshots are made from them
(MOD92).** Six listings: English, Brazilian Portuguese, German, French, Spanish and Simplified
Chinese. The package declares English alone (`Resources` in `AppxManifest.xml`), so the other five are
added under Store listings > **Manage additional languages**. Until the interface is translated
(Block K), each non-English description ends by saying the interface is in English.

Each section keeps its shape, or the scripts stop and name it: four bold headers (short
description, description, features, screenshot captions) and seven `**Label:**` lines (keywords,
copyright, the photo label, the trailer's line over the viewer's modes and its closing line, the badge
that every screenshot and trailer frame carries saying it all runs on the PC, and the trailer's
scene saying so). A caption line is the screenshot's headline, its stressed words in
[brackets], then ` | ` and the smaller line under it; the Store's caption is the headline alone,
without the brackets.

1. `powershell -NoProfile -File build\store-shots-vm.ps1` captures the window on the VMware guest once
   and composes it in every language, into `artifacts\store-shots\<language>\screenshot-1..4.png`.
2. In Partner Center, Store listings > Export listings gives the CSV; `powershell -NoProfile -File
   build\store-listing-csv.ps1 -Template <that .csv>` fills every language's text from this file into
   `artifacts\store-listing\`, to import back. The import refuses images named by file, so the
   screenshots go up on each language's page (or through the submission API).

The first submission of an app can only be made on the Partner Center page: the submission API
creates later ones by copying the last published. Store ID `9PB86WW5NRR2`, listed since
11/11/2026 at https://apps.microsoft.com/detail/9PB86WW5NRR2.

## Files

| What | Where it comes from |
|---|---|
| Package | `artifacts\msix\Modelado.msix` from `build\build-msix.cmd`, checked with `build\check-msix.ps1 -Layout artifacts\msix\layout -Cases` |
| Screenshots, 1920 x 1080 | `artifacts\store-shots\<language>\screenshot-1..4.png` from `build\store-shots-vm.ps1` (after `build\publish.cmd`): the window captured on the VMware guest (`raw\`), then composed under its caption by `tools\Modelado.Icon --store-shot` (MOD81) |
| Box art 1:1 | `artifacts\store-art\box-1x1-2160x2160.png` |
| Poster art 9:16 | `artifacts\store-art\poster-9x16-1440x2160.png` |
| Super hero art 16:9 | `artifacts\store-art\hero-16x9-3840x2160.png`: the chair Modelado made, drawn by its own renderer, no text since the Store sets the title over it; the listing plays a trailer only when this is there (MOD82) |
| Trailer, per language | `artifacts\store-trailer\<language>\trailer.mp4` (1920 x 1080, 30 fps, 37.5 s) and its thumbnail `trailer.png`, from `build\store-trailer.ps1` after the screenshots (MOD84); the Store plays it only beside the Super hero art |
| App tile icons | `artifacts\store-art\tile-300x300.png`, `tile-150x150.png`, `tile-71x71.png` |

The art is drawn by `dotnet run --project tools\Modelado.Icon -c Release -- --store-art artifacts\store-art`.

## Once per submission

**Pricing:** Free. **Markets:** all.

**Category:** Multimedia design.

**Privacy policy URL:** https://alegauss.github.io/modelado/privacy/

**Website:** https://alegauss.github.io/modelado/

**Support contact:** https://github.com/alegauss/modelado/issues

### Notes for certification

Modelado turns a photo into a 3D model with AI models that run locally in ONNX Runtime (on the GPU
through DirectML, or on the CPU). The models are not in the package: the first time an operation
needs one, the window shows what it will download (size, source, licence) and waits for a yes.
They
are data files fetched from huggingface.co/alegauss/modelado-models, each checked against a pinned
SHA-256 before use, not code.

To test: open any photo (the Open button on the toolbar, or drop it on the window), choose Generate
relief, and accept the one-time download of 50 to 100 MB. A relief appears in the 3D viewer in a few seconds; drag to turn it, and use Export to save
it. Any GLB, OBJ, PLY or STL file can also be opened directly, with no download.

The app sends no telemetry and has no account. Meshy AI and Ideogram are optional cloud services
that work only with the user's own API key, entered in About; each job shows what is sent and its
price and waits for consent. Updates come from the Store: the package never checks GitHub for
releases. The `modelado-cli.exe` execution alias lets the optional Blender add-on and a terminal
run the command line and open the window (`modelado-cli window`).

### Restricted capability: runFullTrust

Partner Center's field takes about 500 characters and cuts the rest silently. What went with the
first submission (11/11/2026):

> Modelado is a Win32 desktop app (.NET, WPF) packaged as MSIX. It runs as the user, never
> elevated. Full trust lets it run AI models locally with ONNX Runtime and DirectML on the GPU,
> open and save photos and 3D models wherever the user picks, and start Blender or be started by
> its Blender add-on through the modelado-cli alias. Photos never leave the PC; no data goes to
> the developer.

The longer answer, for a reviewer who asks:

> Modelado is a classic Win32 desktop application (.NET 10, WPF) packaged as MSIX, so it runs as
> Windows.FullTrustApplication. It does not elevate, install services or drivers, and runs entirely
> as the signed-in user. It needs full trust for:
> 1. ONNX Runtime with DirectML, which loads native libraries and uses the GPU through DirectX 12 to
>    run the AI models on the user's computer.
> 2. Opening and saving 3D models and photos anywhere the user picks, and the file associations for
>    .glb, .obj, .ply and .stl.
> 3. Starting Blender with the model shown (Open in Blender), and being started by the Blender
>    add-on and from a terminal through the modelado-cli.exe execution alias.
>
> No data is sent to the developer.

### Age rating questionnaire (IARC)

The app has no violence, sexual content, gambling, purchases, user accounts, chat or sharing
between users, and collects no location. The answer to "unrestricted internet access" is no: it
downloads its own models and, with the user's key, calls two cloud services. The photos and models
are the user's own files.

### AI-generated content

The local models reconstruct geometry from the user's own photo. The optional Ideogram texture and
Meshy AI text-to-3D jobs generate content from text the user types, under those services' own
content policies, with the user's own account. Reports about generated content go to the issues
link above. Check this against the Store policy current on the day of submission.

---

## English (en-us)

**Short description**

Turn a photo into a 3D model on your own PC: a whole object or a relief, cleaned up and exported
for printing, games or the web. Nothing is uploaded.

**Description**

Modelado turns photos into 3D models with AI that runs on your own Windows computer, on the GPU when
it has one. Your photos are never uploaded.

One photo of an object becomes a whole coloured model with TripoSR, its sides and back inferred.
Any photo becomes a bas-relief with Depth Anything V2, textured with the photo and ready to print
with a solid base.

Then refine it: simplify, subdivide, smooth, fill holes, remove loose pieces, flatten a base, turn
and mirror, with every step undoable. Real-ESRGAN redraws a texture four times sharper. Open any
GLB, OBJ, PLY or STL to edit it too, and export to GLB, OBJ, PLY or STL sized in millimetres.

Each AI model is downloaded once, after the app says how large it is and where it comes from, and
is checked by SHA-256. A Blender add-on sends objects to Modelado and back. Meshy AI and Ideogram
can be added with your own key, and nothing is sent to them without your yes.

**Product features**

- A whole 3D object from one photo (TripoSR)
- A printable relief from any photo (Depth Anything V2)
- Simplify, subdivide, smooth, fill holes and flatten a base, all undoable
- 4x sharper textures with Real-ESRGAN
- Export to GLB, OBJ, PLY or STL, sized in millimetres for printing
- Runs on your PC, on the GPU through DirectML or on the CPU
- No account and no telemetry; your photos stay on your PC

**Keywords:** 3D model, photo to 3D, 3D printing, relief, GLB, STL, AI

**Copyright and trademark:** © Alegauss. The AI models carry their own licences, listed in About.

**Photo label:** Your photo

**Trailer views:** Turn it round, [in colour, clay or wireframe] | The app's own viewer, on your PC

**Trailer end:** Free on the Microsoft Store

**Local badge:** Runs on your PC · Nothing uploaded

**Trailer, local:** Everything runs [on your PC] | No upload, no account: each AI model is downloaded once, then runs on your GPU or CPU

**Screenshot captions**

1. One photo becomes [a whole 3D model] | TripoSR infers the sides and the back, on your own PC
2. Any photo becomes [a relief] | Depth Anything V2 turns depth into height, textured with the photo
3. Refine it, [every step undoable] | Simplify, subdivide, smooth, fill holes and flatten a base
4. Export to GLB, OBJ, PLY or STL, [sized in millimetres] | Ready for a 3D printer, a game engine, Blender or the web

## Português (Brasil) (pt-br)

**Descrição curta**

Transforme uma foto em um modelo 3D no seu próprio PC: um objeto inteiro ou um relevo, limpo e
exportado para impressão 3D, jogos ou a web. Nada é enviado para a internet.

**Descrição**

O Modelado transforma fotos em modelos 3D com uma IA que roda no seu próprio computador com Windows,
na GPU quando houver uma. Suas fotos nunca são enviadas para a internet.

Uma foto de um objeto vira um modelo inteiro e colorido com o TripoSR, que deduz as laterais e o
verso. Qualquer foto vira um baixo-relevo com o Depth Anything V2, com a textura da própria foto e
pronto para imprimir sobre uma base sólida.

Depois é só refinar: simplificar, subdividir, suavizar, fechar buracos, remover pedaços soltos,
aplainar a base, girar e espelhar, e cada passo pode ser desfeito. O Real-ESRGAN redesenha uma
textura quatro vezes mais nítida. Abra também qualquer GLB, OBJ, PLY ou STL para editar, e exporte em
GLB, OBJ, PLY ou STL com as medidas em milímetros.

Cada modelo de IA é baixado uma vez só, depois de o app dizer o tamanho e de onde ele vem, e é
conferido por SHA-256. Um add-on do Blender manda objetos para o Modelado e de volta. O Meshy AI e o
Ideogram podem ser adicionados com a sua própria chave, e nada é enviado a eles sem o seu sim.

A interface do app está em inglês por enquanto.

**Recursos do produto**

- Um objeto 3D inteiro a partir de uma foto (TripoSR)
- Um relevo para imprimir a partir de qualquer foto (Depth Anything V2)
- Simplificar, subdividir, suavizar, fechar buracos e aplainar a base, tudo desfazível
- Texturas 4 vezes mais nítidas com o Real-ESRGAN
- Exporta em GLB, OBJ, PLY ou STL, com medidas em milímetros para impressão
- Roda no seu PC, na GPU via DirectML ou na CPU
- Sem conta e sem telemetria; suas fotos ficam no seu PC

**Palavras-chave:** modelo 3D, foto para 3D, impressão 3D, relevo, GLB, STL, IA

**Direitos autorais e marcas:** © Alegauss. Os modelos de IA têm licenças próprias, listadas em About.

**Rótulo da foto:** Sua foto

**Trailer, visualização:** Gire o modelo, [em cores, argila ou wireframe] | O visualizador do próprio app, no seu PC

**Trailer, final:** Grátis na Microsoft Store

**Selo local:** Roda no seu PC · Nada é enviado

**Trailer, local:** Tudo roda [no seu PC] | Sem upload, sem conta: cada modelo de IA é baixado uma vez e roda na sua GPU ou CPU

**Legendas das capturas de tela**

1. Uma foto vira [um modelo 3D inteiro] | O TripoSR deduz as laterais e o verso, no seu próprio PC
2. Qualquer foto vira [um relevo] | O Depth Anything V2 transforma profundidade em altura, com a textura da foto
3. Refine, [cada passo pode ser desfeito] | Simplifique, subdivida, suavize, feche buracos e aplaine a base
4. Exporte em GLB, OBJ, PLY ou STL, [em milímetros] | Pronto para impressora 3D, motor de jogo, Blender ou a web

## Deutsch (de-de)

**Kurzbeschreibung**

Verwandeln Sie ein Foto auf Ihrem eigenen PC in ein 3D-Modell: ein ganzes Objekt oder ein Relief,
bereinigt und exportiert für den 3D-Druck, für Spiele oder das Web. Nichts wird hochgeladen.

**Beschreibung**

Modelado verwandelt Fotos in 3D-Modelle, mit KI, die auf Ihrem eigenen Windows-Computer läuft, auf
der GPU, wenn eine vorhanden ist. Ihre Fotos werden nie hochgeladen.

Aus dem Foto eines Objekts wird mit TripoSR ein vollständiges, farbiges Modell; Seiten und Rückseite
werden abgeleitet. Aus jedem Foto wird mit Depth Anything V2 ein Flachrelief, mit dem Foto als Textur
und auf einem massiven Sockel druckfertig.

Danach verfeinern Sie es: vereinfachen, unterteilen, glätten, Löcher füllen, lose Teile entfernen,
einen Sockel abflachen, drehen und spiegeln, und jeder Schritt lässt sich rückgängig machen.
Real-ESRGAN zeichnet eine Textur viermal schärfer. Öffnen Sie auch jede GLB-, OBJ-, PLY- oder
STL-Datei zum Bearbeiten, und exportieren Sie nach GLB, OBJ, PLY oder STL, in Millimetern bemaßt.

Jedes KI-Modell wird nur einmal heruntergeladen, nachdem die App gesagt hat, wie groß es ist und
woher es kommt, und per SHA-256 geprüft. Ein Blender-Add-on schickt Objekte an Modelado und zurück.
Meshy AI und Ideogram lassen sich mit Ihrem eigenen Schlüssel hinzufügen, und ohne Ihre Zustimmung
wird nichts an sie gesendet.

Die Oberfläche der App ist vorerst auf Englisch.

**Produktfunktionen**

- Ein ganzes 3D-Objekt aus einem Foto (TripoSR)
- Ein druckbares Relief aus jedem Foto (Depth Anything V2)
- Vereinfachen, unterteilen, glätten, Löcher füllen und einen Sockel abflachen, alles umkehrbar
- Viermal schärfere Texturen mit Real-ESRGAN
- Export nach GLB, OBJ, PLY oder STL, in Millimetern für den 3D-Druck
- Läuft auf Ihrem PC, auf der GPU über DirectML oder auf der CPU
- Kein Konto und keine Telemetrie; Ihre Fotos bleiben auf Ihrem PC

**Suchbegriffe:** 3D-Modell, Foto zu 3D, 3D-Druck, Relief, GLB, STL, KI

**Urheberrecht und Marken:** © Alegauss. Die KI-Modelle haben eigene Lizenzen, aufgeführt unter About.

**Fotobeschriftung:** Ihr Foto

**Trailer, Ansichten:** Rundum drehen, [in Farbe, Ton oder Drahtgitter] | Der eigene Viewer der App, auf Ihrem PC

**Trailer, Schluss:** Kostenlos im Microsoft Store

**Lokal-Siegel:** Läuft auf Ihrem PC · Kein Upload

**Trailer, lokal:** Alles läuft [auf Ihrem PC] | Kein Upload, kein Konto: Jedes KI-Modell wird einmal geladen und läuft dann auf Ihrer GPU oder CPU

**Bildunterschriften**

1. Aus einem Foto wird [ein ganzes 3D-Modell] | TripoSR leitet Seiten und Rückseite ab, auf Ihrem eigenen PC
2. Aus jedem Foto wird [ein Relief] | Depth Anything V2 macht Tiefe zu Höhe, mit dem Foto als Textur
3. Verfeinern, [jeder Schritt umkehrbar] | Vereinfachen, unterteilen, glätten, Löcher füllen, Sockel abflachen
4. Export nach GLB, OBJ, PLY oder STL, [in Millimetern] | Bereit für 3D-Drucker, Spiele-Engines, Blender und das Web

## Français (fr-fr)

**Description courte**

Transformez une photo en modèle 3D sur votre propre PC : un objet entier ou un relief, nettoyé et
exporté pour l'impression 3D, les jeux ou le web. Rien n'est envoyé en ligne.

**Description**

Modelado transforme des photos en modèles 3D grâce à une IA qui tourne sur votre propre ordinateur
Windows, sur le GPU s'il y en a un. Vos photos ne sont jamais envoyées en ligne.

La photo d'un objet devient un modèle entier et en couleurs avec TripoSR, qui en déduit les côtés et
le dos. N'importe quelle photo devient un bas-relief avec Depth Anything V2, texturé avec la photo et
prêt à imprimer sur un socle plein.

Ensuite, affinez-le : simplifier, subdiviser, lisser, combler les trous, retirer les morceaux isolés,
aplanir une base, tourner et retourner en miroir, chaque étape pouvant être annulée. Real-ESRGAN
redessine une texture quatre fois plus nette. Ouvrez aussi n'importe quel fichier GLB, OBJ, PLY ou
STL pour le modifier, et exportez en GLB, OBJ, PLY ou STL, dimensionné en millimètres.

Chaque modèle d'IA n'est téléchargé qu'une fois, après que l'application a indiqué sa taille et sa
provenance, et il est vérifié par SHA-256. Un module complémentaire pour Blender envoie des objets
vers Modelado et les récupère. Meshy AI et Ideogram peuvent être ajoutés avec votre propre clé, et
rien ne leur est envoyé sans votre accord.

L'interface de l'application est pour l'instant en anglais.

**Fonctionnalités du produit**

- Un objet 3D entier à partir d'une seule photo (TripoSR)
- Un relief imprimable à partir de n'importe quelle photo (Depth Anything V2)
- Simplifier, subdiviser, lisser, combler les trous et aplanir une base, tout est annulable
- Des textures quatre fois plus nettes avec Real-ESRGAN
- Export en GLB, OBJ, PLY ou STL, dimensionné en millimètres pour l'impression
- Fonctionne sur votre PC, sur le GPU via DirectML ou sur le CPU
- Ni compte ni télémétrie ; vos photos restent sur votre PC

**Mots-clés :** modèle 3D, photo en 3D, impression 3D, relief, GLB, STL, IA

**Droits d'auteur et marques :** © Alegauss. Les modèles d'IA ont leurs propres licences, listées dans About.

**Légende de la photo :** Votre photo

**Bande-annonce, vues :** Faites-le tourner, [en couleurs, argile ou filaire] | La visionneuse de l'application, sur votre PC

**Bande-annonce, fin :** Gratuit sur le Microsoft Store

**Badge local :** Fonctionne sur votre PC · Rien n'est envoyé

**Bande-annonce, local :** Tout se passe [sur votre PC] | Ni envoi ni compte : chaque modèle d'IA est téléchargé une fois, puis tourne sur votre GPU ou CPU

**Légendes des captures d'écran**

1. Une photo devient [un modèle 3D entier] | TripoSR en déduit les côtés et le dos, sur votre propre PC
2. Toute photo devient [un relief] | Depth Anything V2 change la profondeur en hauteur, texturée avec la photo
3. Affinez-le, [chaque étape annulable] | Simplifier, subdiviser, lisser, combler les trous, aplanir une base
4. Export en GLB, OBJ, PLY ou STL, [en millimètres] | Pour une imprimante 3D, un moteur de jeu, Blender ou le web

## Español (es-es)

**Descripción breve**

Convierte una foto en un modelo 3D en tu propio PC: un objeto completo o un relieve, limpio y
exportado para impresión 3D, juegos o la web. No se sube nada.

**Descripción**

Modelado convierte fotos en modelos 3D con una IA que se ejecuta en tu propio ordenador con Windows,
en la GPU si la tiene. Tus fotos nunca se suben a internet.

La foto de un objeto se convierte en un modelo completo y en color con TripoSR, que deduce sus lados
y su parte trasera. Cualquier foto se convierte en un bajorrelieve con Depth Anything V2, con la
textura de la foto y listo para imprimir sobre una base sólida.

Después, refínalo: simplifica, subdivide, suaviza, rellena agujeros, quita piezas sueltas, aplana una
base, gira y refleja, y cada paso se puede deshacer. Real-ESRGAN redibuja una textura cuatro veces
más nítida. Abre también cualquier GLB, OBJ, PLY o STL para editarlo, y exporta a GLB, OBJ, PLY o
STL con medidas en milímetros.

Cada modelo de IA se descarga una sola vez, después de que la aplicación indique su tamaño y su
origen, y se comprueba con SHA-256. Un complemento de Blender envía objetos a Modelado y de vuelta.
Meshy AI e Ideogram se pueden añadir con tu propia clave, y no se les envía nada sin tu
consentimiento.

Por ahora, la interfaz de la aplicación está en inglés.

**Características del producto**

- Un objeto 3D completo a partir de una foto (TripoSR)
- Un relieve imprimible a partir de cualquier foto (Depth Anything V2)
- Simplificar, subdividir, suavizar, rellenar agujeros y aplanar una base, todo se puede deshacer
- Texturas cuatro veces más nítidas con Real-ESRGAN
- Exporta a GLB, OBJ, PLY o STL, con medidas en milímetros para imprimir
- Se ejecuta en tu PC, en la GPU con DirectML o en la CPU
- Sin cuenta y sin telemetría; tus fotos se quedan en tu PC

**Palabras clave:** modelo 3D, foto a 3D, impresión 3D, relieve, GLB, STL, IA

**Derechos de autor y marcas:** © Alegauss. Los modelos de IA tienen sus propias licencias, indicadas en About.

**Rótulo de la foto:** Tu foto

**Tráiler, vistas:** Gíralo, [en color, arcilla o alambre] | El visor de la propia aplicación, en tu PC

**Tráiler, final:** Gratis en Microsoft Store

**Sello local:** Funciona en tu PC · No se sube nada

**Tráiler, local:** Todo funciona [en tu PC] | Sin subidas ni cuentas: cada modelo de IA se descarga una vez y funciona en tu GPU o CPU

**Leyendas de las capturas de pantalla**

1. Una foto se convierte en [un modelo 3D completo] | TripoSR deduce los lados y la parte trasera, en tu propio PC
2. Cualquier foto se convierte en [un relieve] | Depth Anything V2 convierte la profundidad en altura, con la textura de la foto
3. Refínalo, [cada paso se puede deshacer] | Simplifica, subdivide, suaviza, rellena agujeros y aplana una base
4. Exporta a GLB, OBJ, PLY o STL, [en milímetros] | Para una impresora 3D, un motor de juegos, Blender o la web

## 中文（简体） (zh-cn)

**简短说明**

在你自己的电脑上把一张照片变成 3D 模型：完整的物体或浮雕，经过清理后导出，用于 3D 打印、游戏或网页。不会上传任何内容。

**说明**

Modelado 借助在你自己的 Windows 电脑上运行的 AI 把照片变成 3D 模型，有 GPU 时就在 GPU 上运行。你的照片从不上传。

用 TripoSR，一张物体照片就能变成完整的彩色模型，侧面和背面由模型推断。用 Depth Anything V2，任何照片都能变成浅浮雕，以照片为纹理，加上实心底座即可打印。

然后进行精修：简化、细分、平滑、补洞、移除零散碎块、压平底座、旋转和镜像，每一步都可以撤销。Real-ESRGAN 能把纹理重绘得清晰四倍。你也可以打开任何 GLB、OBJ、PLY 或 STL 文件进行编辑，并按毫米尺寸导出为 GLB、OBJ、PLY 或 STL。

每个 AI 模型只下载一次，下载前应用会说明它的大小和来源，并用 SHA-256 校验。Blender 插件可以把物体发送到 Modelado 再传回。使用你自己的密钥可以添加 Meshy AI 和 Ideogram，未经你同意不会向它们发送任何内容。

应用界面目前为英文。

**产品功能**

- 一张照片生成完整的 3D 物体（TripoSR）
- 任何照片都能生成可打印的浮雕（Depth Anything V2）
- 简化、细分、平滑、补洞和压平底座，全部可撤销
- 用 Real-ESRGAN 让纹理清晰四倍
- 导出为 GLB、OBJ、PLY 或 STL，按毫米尺寸便于打印
- 在你的电脑上运行，通过 DirectML 使用 GPU，或使用 CPU
- 无需账户，没有遥测；你的照片留在你的电脑上

**关键字:** 3D 模型, 照片转 3D, 3D 打印, 浮雕, GLB, STL, AI

**版权和商标:** © Alegauss。AI 模型有各自的许可证，列在 About 中。

**照片标签:** 你的照片

**预告片视图:** 旋转查看，[彩色、白模或线框] | 应用自带的查看器，在你的电脑上运行

**预告片结尾:** 在 Microsoft Store 免费获取

**本地标识:** 在你的电脑上运行 · 不上传任何内容

**预告片本地:** 一切都[在你的电脑上]完成 | 无需上传，无需账户：每个 AI 模型只下载一次，然后在你的 GPU 或 CPU 上运行

**屏幕截图说明**

1. 一张照片变成[完整的 3D 模型] | TripoSR 推断侧面和背面，全部在你自己的电脑上完成
2. 任何照片都能变成[浮雕] | Depth Anything V2 把深度变成高度，并以照片为纹理
3. 精修模型，[每一步都可撤销] | 简化、细分、平滑、补洞、压平底座
4. 导出为 GLB、OBJ、PLY 或 STL，[按毫米尺寸] | 适用于 3D 打印机、游戏引擎、Blender 或网页
