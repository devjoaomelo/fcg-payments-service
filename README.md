# FCG.Payments.Api

> Microserviço de processamento de pagamentos do FIAP Cloud Games — .NET 8 com Event-Driven Architecture, SQS, SNS e Lambda

##  Sobre o Projeto

A **FCG.Payments.Api** é o microserviço responsável pelo processamento de pagamentos da plataforma FIAP Cloud Games. Desenvolvida com arquitetura orientada a eventos (Event-Driven Architecture), oferece:

-  **Criação de pagamentos** com validação de preços via Games API
-  **Confirmação de pagamentos** (manual admin ou automática via Lambda)
-  **Notificações** via AWS SNS quando pagamentos são confirmados
-  **Processamento automático** via AWS Lambda com trigger SQS
-  **Event Sourcing** completo (timeline de eventos por pagamento)
-  **Autenticação JWT** com controle de acesso (user vs admin)
-  **Observabilidade**: logs estruturados, métricas e tracing distribuído

##  Arquitetura


###  Estrutura do Projeto (Clean Architecture)

```
FCG.Payments/
├── src/
│   ├── FCG.Payments.Api/              #  Presentation Layer
│   │   ├── Program.cs                 # Bootstrap, DI, Endpoints
│   │   └── appsettings.json
│   │
│   ├── FCG.Payments.Application/      # 💼 Application Layer
│   │   ├── UseCases/
│   │   │   ├── Create/                # Criar pagamento
│   │   │   │   └── CreatePaymentHandler.cs
│   │   │   ├── Confirm/               # Confirmar pagamento
│   │   │   │   └── ConfirmPaymentHandler.cs
│   │   │   ├── Get/                   # Buscar por ID
│   │   │   │   └── GetPaymentHandler.cs
│   │   │   └── List/                  # Listar (admin)
│   │   │       └── ListPaymentsHandler.cs
│   │   ├── Events/
│   │   │   ├── PaymentRequested.cs
│   │   │   └── PaymentConfirmed.cs
│   │   └── Interfaces/
│   │       ├── IEventStore.cs
│   │       ├── IGamesCatalogClient.cs  # HTTP Client para Games API
│   │       ├── IMessageBus.cs          # Abstração SQS
│   │       └── INotificationPublisher.cs # Abstração SNS
│   │
│   ├── FCG.Payments.Domain/           #  Domain Layer
│   │   ├── Entities/
│   │   │   └── Payment.cs             # Agregado raiz
│   │   ├── Enums/
│   │   │   └── PaymentStatus.cs       # Pending | Paid | Failed
│   │   └── Interfaces/
│   │       └── IPaymentRepository.cs
│   │
│   └── FCG.Payments.Infra/            #  Infrastructure Layer
│       ├── Data/
│       │   ├── PaymentsDbContext.cs   # EF Core Context
│       │   └── EventRecord.cs         # Entidade Event Store
│       ├── Repositories/
│       │   ├── MySqlPaymentRepository.cs
│       │   └── PaymentReadRepository.cs
│       ├── Clients/
│       │   └── GamesCatalogClient.cs  # HTTP Client para Games
│       └── Messaging/
│           ├── SqsMessageBus.cs       # Implementação SQS
│           ├── SnsNotificationPublisher.cs # Implementação SNS
│           └── EventStoreEf.cs        # Event Sourcing com EF
│
├── .aws/
│   └── ecs-taskdef.json              # Task Definition ECS
├── .github/
│   └── workflows/
│       ├── ci.yml                    # Pipeline de testes
│       ├── cd.yml                    # Deploy automático ECS
│       └── docker.yml                # Docker Hub
└── lambda/
    └── payment-processor/            # ⚡ Lambda Function
        └── index.js                  # Handler SQS → MySQL → SNS
```

##  Endpoints da API

###  Health Checks

```http
# health
GET /health
Response: 200 OK { "status": "Healthy" }

# verifica MySQL
GET /health/db
Response: 200 OK | 503 Service Unavailable

# Versão da API
GET /version
Response: 200 OK { "service": "fcg-payments-service", "version": "1.0.0" }
```

###  Pagamentos (Usuário Autenticado)

#### Criar pagamento
```http
POST /api/payments
Authorization: Bearer {jwt_token}
Content-Type: application/json

{
  "gameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}

Response: 201 Created
{
  "id": "7b2e4d1c-...",
  "userId": "9a8f7e6d-...",
  "gameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "amount": 199.90,
  "status": "Pending",
  "createdAtUtc": "2025-10-10T10:30:00Z"
}
```

**Fluxo interno:**
1.  Valida JWT e extrai `userId`
2.  Consulta preço do jogo na Games API
3.  Cria payment no MySQL com status `Pending`
4.  Registra evento `PaymentRequested` no Event Store
5.  Publica mensagem na fila SQS `fcg-payments-requested`
6.  Retorna response 201

#### Buscar pagamento por ID
```http
GET /api/payments/{id}
Authorization: Bearer {jwt_token}

Response: 200 OK
{
  "id": "7b2e4d1c-...",
  "userId": "9a8f7e6d-...",
  "gameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "amount": 199.90,
  "status": "Paid",
  "createdAtUtc": "2025-10-10T10:30:00Z",
  "updatedAtUtc": "2025-10-10T10:35:00Z"
}

Response: 404 Not Found (se não pertencer ao usuário)
```

**Regra de negócio:**
- Usuário comum: só vê próprios pagamentos
- Admin: vê todos os pagamentos

###  Pagamentos (Admin)

#### Confirmar pagamento (manual)
```http
POST /api/payments/{id}/confirm
Authorization: Bearer {admin_jwt_token}

Response: 204 No Content (sucesso)
Response: 404 Not Found (payment não existe)
```

**Usado quando:** Processamento manual ou retry de falhas

#### Listar todos os pagamentos
```http
GET /api/payments?page=1&size=20
Authorization: Bearer {admin_jwt_token}

Response: 200 OK
{
  "page": 1,
  "size": 20,
  "count": 20,
  "items": [
    {
      "id": "7b2e4d1c-...",
      "userId": "9a8f7e6d-...",
      "gameId": "3fa85f64-...",
      "amount": 199.90,
      "status": "Paid",
      "createdAtUtc": "2025-10-10T10:30:00Z"
    }
  ]
}
```

###  Endpoints Internos

#### Confirmar pagamento (Lambda → API)
```http
POST /internal/payments/{id}/confirm
X-Internal-Token: {shared_secret}

Response: 204 No Content
Response: 401 Unauthorized (token inválido)
Response: 404 Not Found
```

**Usado pelo:** Lambda function para chamar a API internamente

```
##  Configuração

### Variáveis de Ambiente

| Variável | Descrição | Produção (AWS) | Local |
|----------|-----------|----------------|-------|
| `ASPNETCORE_URLS` | Endereço de binding | `http://+:8080` | `http://+:8080` |
| `ConnectionStrings__PaymentsDb` | MySQL connection string | SSM Parameter | `Server=127.0.0.1;Port=3319;...` |
| `Jwt__Key` | Chave JWT compartilhada | SSM Parameter | `DEV_ONLY_...` |
| `Jwt__Issuer` | Emissor JWT | `fcg-users` | `fcg-users` |
| `Jwt__Audience` | Audiência JWT | `fcg-clients` | `fcg-clients` |
| `GamesApi__BaseUrl` | URL da Games API | `http://alb-fcg-games...` | `http://localhost:8082` |
| `AWS__Region` | Região AWS | `us-east-2` | `us-east-2` |
| `Queues__PaymentsRequested` | SQS Queue URL | `https://sqs.us-east-2...` | (mesma) |
| `Notifications__TopicArn` | SNS Topic ARN | `arn:aws:sns:us-east-2:...` | (mesmo) |
| `InternalAuth__Token` | Token para endpoints internos | SSM Parameter | `dev-internal-token` |
| `Swagger__EnableUI` | Habilitar Swagger UI | `true` | `true` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Endpoint OTLP | `http://127.0.0.1:4317` | `http://127.0.0.1:4317` |
| `OTEL_SERVICE_NAME` | Nome no X-Ray | `FCG.Payments.Api` | `FCG.Payments.Api` |

###  AWS Systems Manager Parameters

```bash
# Connection string MySQL
arn:aws:ssm:us-east-2:536765581095:parameter/fcg/payments/ConnectionStrings__PaymentsDb

# Chave JWT compartilhada
arn:aws:ssm:us-east-2:536765581095:parameter/fcg/users/Jwt__Key

# Token interno (Lambda → API)
arn:aws:ssm:us-east-2:536765581095:parameter/fcg/payments/InternalAuth__Token
```

### AWS SQS Queue

**Nome:** `fcg-payments-requested`
**URL:** `https://sqs.us-east-2.amazonaws.com/536765581095/fcg-payments-requested`

**Configuração:**
- **Visibility Timeout:** 30 segundos
- **Message Retention:** 14 dias
- **Dead Letter Queue:** `fcg-payments-dlq` (após 3 tentativas)
- **Encryption:** AES-256 (SSE-SQS)

### AWS SNS Topic

**Nome:** `fcg-notifications`
**ARN:** `arn:aws:sns:us-east-2:536765581095:fcg-notifications`

**Subscriptions:**
- **Email:** `admin@fiapcloudgames.com`
- **Lambda:** (opcional) para processamento adicional
- **SQS:** (opcional) para auditoria

##  Desenvolvimento Local

### 🐳 Executar com Docker Compose

```bash
# Subir infraestrutura local
docker compose up -d mysql-payments payments

# Verificar logs
docker compose logs -f payments

# Acessar serviços
# API: http://localhost:8083
# Swagger: http://localhost:8083/swagger
# MySQL: localhost:3319
```

### Executar localmente

```bash
# 1. Restaurar dependências
dotnet restore

# 2. Aplicar migrations
cd src/FCG.Payments.Api
dotnet ef database update

# 3. Executar aplicação
dotnet run --project src/FCG.Payments.Api

# 4. Acessar Swagger
# http://localhost:5253/swagger
```

###  Executar testes

```bash
# Todos os testes
dotnet test
```

##  Event Sourcing

### Eventos Registrados

#### PaymentRequested
```json
{
  "paymentId": "7b2e4d1c-...",
  "userId": "9a8f7e6d-...",
  "gameId": "3fa85f64-...",
  "amount": 199.90,
  "createdAtUtc": "2025-10-10T10:30:00Z"
}
```

#### PaymentConfirmed
```json
{
  "paymentId": "7b2e4d1c-...",
  "userId": "9a8f7e6d-...",
  "gameId": "3fa85f64-...",
  "amount": 199.90,
  "confirmedAtUtc": "2025-10-10T10:35:00Z"
}
```

### Consultar Timeline

```sql
SELECT 
  id,
  aggregate_id AS payment_id,
  type,
  data,
  created_at_utc
FROM event_store
WHERE aggregate_id = '7b2e4d1c-...'
ORDER BY created_at_utc ASC;
```

##  Autenticação e Autorização

### Roles e Permissões

| Endpoint | Role Required | Descrição |
|----------|---------------|-----------|
| `POST /api/payments` | `User` (autenticado) | Criar pagamento |
| `GET /api/payments/{id}` | `User` (próprio) ou `Admin` | Buscar payment |
| `POST /api/payments/{id}/confirm` | `Admin` | Confirmar manual |
| `GET /api/payments` | `Admin` | Listar todos |

### Obter Token JWT (via Users API)

```bash
# 1. Criar usuário
curl -X POST http://localhost:8081/api/users \
  -H "Content-Type: application/json" \
  -d '{"name":"João","email":"joao@test.com","password":"senha123"}'

# 2. Login (obter token)
curl -X POST http://localhost:8081/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"joao@test.com","password":"senha123"}'

# Response:
# { "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." }
```

### Usar Token nas Requisições

```bash
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."

curl -X POST http://localhost:8083/api/payments \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"gameId":"3fa85f64-5717-4562-b3fc-2c963f66afa6"}'
```

##  Observabilidade

### Logs Estruturados (Serilog)

```json
{
  "@t": "2025-10-10T10:30:45.123Z",
  "@mt": "Payment {PaymentId} created for user {UserId}",
  "@l": "Information",
  "PaymentId": "7b2e4d1c-...",
  "UserId": "9a8f7e6d-...",
  "CorrelationId": "550e8400-e29b-41d4-a716-446655440000",
  "ServiceName": "fcg-payments",
  "Environment": "Production"
}
```

**Visualizar logs:**
```bash
# CloudWatch Logs
aws logs tail /ecs/fcg-payments --follow --region us-east-2

# Docker local
docker compose logs -f payments
```

### Tracing Distribuído

**Spans capturados:**
- HTTP Request (ASP.NET Core)
- HTTP Client call para Games API
- EF Core queries (MySQL)
- SQS SendMessage
- SNS Publish

### Métricas

**CloudWatch Dashboard:**
- CPU e memória ECS
- Latência P50/P95/P99
- Taxa de requisições
- health

**Alarme:**
```
arn:aws:cloudwatch:us-east-2:536765581095:alarm:FCG-Payments-Health
```

##  Deploy

### CI/CD Pipelines

#### Pipeline CI
```yaml
name: CI
on: [pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet restore
      - run: dotnet build -c Release
      - run: dotnet test -c Release
```

#### Pipeline CD
```yaml
name: CD to ECS
on:
  push:
    branches: [main]

jobs:
  deploy:
    steps:
      - name: Build & Push to ECR
      - name: Update Task Definition
      - name: Deploy to ECS
        wait-for-service-stability: true
```

### Deploy Manual

```bash
# 1. Build image
docker build -t fcg-payments:latest .

# 2. Tag para ECR
aws ecr get-login-password --region us-east-2 | \
  docker login --username AWS --password-stdin 536765581095.dkr.ecr.us-east-2.amazonaws.com

docker tag fcg-payments:latest \
  536765581095.dkr.ecr.us-east-2.amazonaws.com/fcg-payments:latest

# 3. Push
docker push 536765581095.dkr.ecr.us-east-2.amazonaws.com/fcg-payments:latest

# 4. Force new deployment
aws ecs update-service \
  --cluster fcg-cluster \
  --service fcg-payments-svc \
  --force-new-deployment \
  --region us-east-2
```

##  Integrações

### Games API

**Endpoint usado:**
```http
GET http://alb-fcg-games.../api/games/{gameId}

Response:
{
  "id": "3fa85f64-...",
  "title": "The Last of Us Part II",
  "description": "...",
  "price": 199.90
}
```
João Melo FIAP
