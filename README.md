# Biometric API

API que se comunica com um dispositivo biométrico local nitgen, perfeita para integração com aplicações web.

## Requisitos / Compilando

- Requer que as bibliotecas do SDK eNBioBSP estejam instaladas no sistema.
- Requer **.NET 8 SDK** (runtime + SDK).
- Visual Studio 2022 ou superior (opcional, mas recomendado para desenvolvimento e debugging).
- Se você deseja apenas consumir a API, não é necessário compilar o projeto; basta baixar a versão mais recente da API na página de Lançamentos/Releases, executar o instalador e executar a API localmente.

Observação: você pode verificar/alterar o alvo de framework no arquivo de projeto (`.csproj`) e garantir que o .NET 8 SDK esteja instalado localmente.

## Execução e porta

O prefixo padrão é: `http://localhost:5000/apiservice/`
Você pode alterar a porta em `appsettings.json` ou nas configurações de execução (`launchSettings.json`) se houver conflito com outra aplicação.

---

## Sumário de Endpoints

Abaixo um resumo rápido dos endpoints disponíveis (prefixo base: `/apiservice/`):

- `GET capture-hash` — Captura impressão digital e retorna template (hash), ids dos dedos, qualidade e opcionalmente imagens em base64.
- `GET capture-for-verify` — Captura impressão para verificação e retorna template e imagem; permite parâmetro `window` para estilo da janela de captura.
- `POST match-one-on-one` — Envia um template para comparar com uma captura ao vivo (1:1). Pode retornar a imagem se `img=true`.
- `GET identification` — Realiza identificação 1:N contra o índice em memória; aceita `secuLevel` para ajustar sensibilidade.
- `POST load-to-memory` — Carrega um array de templates com `id` para o índice em memória (usado em identificação 1:N).
- `GET delete-all-from-memory` — Remove todos os templates carregados na memória.
- `GET total-in-memory` — Retorna a quantidade de templates atualmente carregados na memória.
- `GET device-unique-id` — Retorna o ID/serial único do dispositivo biométrico.
- `POST join-templates` — Une dois ou mais templates em um único template combinado.

---

## Arquivos principais

- `Modules/Biometric.cs` — Implementa a lógica principal que conversa com o SDK NBioBSP. Métodos expostos usados pelo controlador:
  - `CaptureHash(bool img = false)` — Captura impressão para enrolamento; parâmetro `img` controla retorno das imagens em base64.
  - `CaptureForVerify(uint windowVisibility = NBioAPI.Type.WINDOW_STYLE.POPUP)` — Captura para verificação; `windowVisibility` define estilo da janela de captura.
  - `IdentifyOneOnOne(JsonObject template, bool img = false, uint windowVisibility = NBioAPI.Type.WINDOW_STYLE.POPUP)` — Verifica 1:1 contra um template fornecido; pode retornar imagem.
  - `Identification(uint secuLevel = NBioAPI.Type.FIR_SECURITY_LEVEL.NORMAL, bool img = false, uint windowVisibility = NBioAPI.Type.WINDOW_STYLE.POPUP)` — Identificação 1:N usando índice em memória; `secuLevel` varia entre 1 e 9.
  - `LoadToMemory(JsonArray fingers)` / `DeleteAllFromMemory()` / `TotalIdsInMemory()` — Gerenciam o índice em memória usado na identificação.
  - `JoinTemplates(JsonArray fingers)` — Une múltiplos templates em um único template combinado.

- `Controllers/APIController.cs` — Mapeia endpoints HTTP para os métodos de `Biometric` e traduz parâmetros de query/body. Parâmetros relevantes:
  - `img` (query) — booleano opcional para retornar imagens em resposta.
  - `window` (query) — inteiro opcional para controlar estilo da janela (`0` POPUP, `1` INVISIBLE, etc.).
  - `secuLevel` (query) — inteiro opcional entre 1 a 9 para ajustar sensibilidade na identificação 1:N.

---

# Mapa da API

Abaixo estão os endpoints expostos pela API e seus retornos.

#### GET: `capture-hash/`

Ativa o dispositivo biométrico para capturar sua impressão digital, caso tudo corra bem imagens da captura atual são salvas localmente no diretório `%temp%/fingers-registered` e é retornado:  
`200 | OK`

```json
{
    "fingers-registered": 1,
    "template": "AAAAAZCXZDSfe34t4f//...",  <------- fingerprint hash
    "fingers-id": [ 1, 6 ], <------- 1 to 5 - right thumb to right pinky: 6 to 10 - left thumb to left pinky
    "quality-FIR": 100, <------- quality value of the fingerprint data with a scale of 0 to 100
    "success": true
}
```

qualquer outra coisa:  
`400 | BAD REQUEST`

```json
{
  "message": "Error on Capture: {nitgen error code}",
  "success": false
}
```

Você pode passar um parâmetro opcional `img` para retornar a imagem da digital. As opções disponíveis são:

- `false`: Retorno padrão, sem a imagem.
- `true`: Retorna a imagem em base64.

Exemplo de uso e retorno:
`/capture-hash?img=true`
`200 | OK`

```json
{
    "fingers-registered": 1,
    "template": "AAAAAZCXZDSfe34t4f//...",  <------- fingerprint hash
    "fingers-id": [ 1, 2 ], <------- 1 to 5 - right thumb to right pinky: 6 to 10 - left thumb to left pinky
    "images": ["base64string1", "base64string2", "..."],  <------- array of base64 encoded images (1 to 5 - right thumb to right pinky: 6 to 10 - left thumb to left pinky)
    "quality-FIR": 100, <------- quality value of the fingerprint data with a scale of 0 to 100
    "success": true
}
```

---

#### GET: `capture-for-verify/`

Ativa o dispositivo biométrico para capturar sua impressão digital para verificação, caso tudo corra bem, retorna:  
`200 | OK`

```json
{
    "template": "AAAAAZCXZDSfe34t4f//...",  <------- fingerprint hash
    "image": "base64",                      <------- fingerprint image
    "success": true
}
```

qualquer outra coisa:  
`400 | BAD REQUEST`

```json
{
  "message": "Error on Capture: {nitgen error code}",
  "success": false
}
```

Você pode passar um parâmetro opcional `window` para definir o estilo da janela de captura. As opções disponíveis são:

- `0` (POPUP): Janela de captura padrão.
- `1` (INVISIBLE): Captura invisível.
- `65536` (NO_FPIMG): Sem imagem de impressão digital.
- `131072` (TOPMOST): Janela sempre no topo.
- `262144` (NO_WELCOME): Sem mensagem de boas-vindas.
- `524288` (NO_TOPMOST): Janela não fica no topo.

Exemplo de uso:
`/capture-for-verify?window=1`

---

#### POST: `match-one-on-one/`

Recebe um template e ativa o dispositivo biométrico para comparar:

##### conteúdo da requisição POST:

```json
{
  "template": "AAAAAZCXZDSfe34t4f//..."
}
```

caso o procedimento de verificação corra bem, retorna:  
`200 | OK`

```json
{
    "message": "Fingerprint matches / Fingerprint doesnt match",
    "success": true/false
}
```

qualquer outra coisa:  
`400 | BAD REQUEST`

```json
{
  "message": "Timeout / Error on Verify: {nitgen error code}",
  "success": false
}
```

Você pode passar um parâmetro opcional `img` para retornar a imagem da digital. As opções disponíveis são:

- `false`: Retorno padrão, sem a imagem.
- `true`: Retorna a imagem em base64.

Exemplo de uso e retorno:
`/match-one-on-one?img=true`
`200 | OK`

```json
{
    "message": "Fingerprint matches / Fingerprint doesnt match",
    "image": "base64",  <------- fingerprint image
    "success": true/false
}
```

Você também pode passar um parâmetro opcional `window` para definir o estilo da janela de captura, as opções disponíveis são as mesmas do endpoint `capture-for-verify`.

---

#### GET: `identification/`

Captura sua impressão digital e faz uma busca no índice (1:N) a partir do banco de dados em memória, caso tudo corra bem:  
`200 | OK`

```json
{
    "message": "Fingerprint match found / Fingerprint match not found",
    "id": id_number,     <------ returns 0 in case its not found
    "success": true/false
}
```

qualquer outra coisa:  
`400 | BAD REQUEST`

```json
{
  "message": "Error on Capture: {nitgen error code}",
  "success": false
}
```

Caso encontre problemas com a validação da impressão digital sendo muito rigorosa ou muito permissível, é possível passar um parâmetro opcional `secuLevel` para reduzir ou aumentar o nível de segurança da validação entre 1 (mínimo) e 9 (máximo), o padrão é 5.

`/identification?secuLevel=9`

Você também pode passar um parâmetro opcional `img` para retornar a imagem da digital, e um parâmetro `window` para definir o estilo da janela de captura, as opções disponíveis são as mesmas do endpoint `capture-for-verify`.

---

#### POST: `load-to-memory/`

Recebe um **array** de templates com ID para carregar na memória do index search:

##### POST REQUEST content:

```json
[
    {
        "id": id_number,        <------ e.g: 1, 2, 3  or 4235, 654646, 23423
        "template": "AAAAAZCXZDSfe34t4f//..."
    },
    {
        "id": id_number,
        "template": "AAAAAZCXZDSfe3ff454t4f//..."
    },
    ...
]
```

caso o procedimento de verificação corra bem, retorna:  
`200 | OK`

```json
{
  "message": "Templates loaded to memory",
  "success": true
}
```

qualquer outra coisa:  
`400 | BAD REQUEST`

```json
{
  "message": "Error on AddFIR: {nitgen error code}",
  "success": false
}
```

---

#### GET: `delete-all-from-memory/`

Exclui todos os dados armazenados na memória para uso no index search, caso tudo corra bem, retorna:  
`200 | OK`

```json
{
  "message": "All templates deleted from memory",
  "success": true
}
```

---

#### GET : `total-in-memory`

Retorna a quantidade de templates armazenados na memória:
`200 | OK`

```json
{
	"total": 0,  <------ total templates
	"success": true
}
```

---

#### GET : `device-unique-id`

Retorna o ID único do dispositivo biométrico:
`200 | OK`

```json
{
	"serial": "FF-FF-FF-FF-FF-FF-FF-FF",  <------ device ID
	"success": true
}
```

---

#### POST : `join-templates`

Recebe dois ou mais templates e retorna um template único com a informação de todos os dedos registrados:

##### POST REQUEST content:

```json
[
    {
        "template": "AAAAAZCXZDSfe34t4f//..."
    },
    {
        "template": "AAAAAZCXZDSfe3ff454t4f//..."
    },
    ...
]
```

caso o procedimento de verificação corra bem, retorna:
`200 | OK`

```json
{
	"template": "AAAAAZCXZDSfe34t4f//...",  <------ combined hash
	"message": "Templates joined successfully",
	"success": true
}
```

qualquer outra coisa:  
`400 | BAD REQUEST`

```json
{
  "message": "Error creating template: {nitgen error code}",
  "success": false
}
