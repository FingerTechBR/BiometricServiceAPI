# Biometric API
API que se comunica com um dispositivo biométrico local nitgen, perfeito para integração com aplicações web.

## Compilando
- Requer que as bibliotecas do SDK eNBioBSP estejam instaladas no sistema.
- .NET 7 ou superior 

# Mapa da API
O prefixo é: `http://localhost:5000/apiservice/`  
Você pode alterar a porta em appsettings.json se precisar em caso de conflito.

#### GET: `capture-hash/`
Ativa o dispositivo biométrico para capturar sua impressão digital, caso tudo corra bem imagens da captura atual são salvas localmente no diretório `%temp%/fingers-registered` e é retornado:  
`200 | OK`
```json
{
    "fingers-registered": 1,
    "template": "AAAAAZCXZDSfe34t4f//...",  <------- fingerprint hash
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

--------------------------------

#### GET: `capture/`
Ativa o dispositivo biométrico para capturar uma amostra apenas da impressão digital para verificação:  
`200 | OK`
```json
{
    "template": "AAAAAZCXZDSfe34t4f//...",  <------- fingerprint hash
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

--------------------------------

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

--------------------------------

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

--------------------------------

#### POST: `load-to-memory/`
Recebe um __array__ de templates com ID para carregar na memória do index search:  
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

------------------------------

#### GET: `delete-all-from-memory/`
Exclui todos os dados armazenados na memória para uso no index search, caso tudo corra bem, retorna:    
`200 | OK`
```json
{
    "message": "All templates deleted from memory",
    "success": true
}
```

--------------------------------

#### GET : `total-in-memory`

Retorna a quantidade de templates armazenados na memória:
`200 | OK`
```json
{
	"total": 0,  <------ total templates
	"success": true
}
```

--------------------------------

#### GET : `device-unique-id`

Retorna o ID único do dispositivo biométrico:
`200 | OK`
```json
{
	"serial": "FF-FF-FF-FF-FF-FF-FF-FF",  <------ device ID
	"success": true
}
```
