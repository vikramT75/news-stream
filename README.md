# Global News Stream

A distributed Hybrid Cloud application that streams live global news into a Kafka event bus, processes article embeddings via a local edge GPU and persists the data in SQL Server utilizing Semantic Search with Temporal Decay algorithms.

During query execution, the system retrieves the most relevant and recent news articles to construct accurate, context-aware responses.

## Architecture Overview: Hybrid Cloud and Edge AI

This project leverages a hybrid deployment model to optimize computational costs while maintaining global availability:
- **Cloud Node on AWS EC2:** Hosts the Client Interface, API, Kafka Cluster and SQL Server backend.
- **Edge Node on a Local Workstation:** Executes Large Language Models like Ollama locally via GPU hardware, maintaining a secure connection to the cloud infrastructure through an Ngrok reverse tunnel.

### Technology Stack
* **Frontend:** Vanilla HTML/JS served via Nginx
* **Backend:** .NET 8 ASP.NET Core API
* **Event Streaming:** Confluent Kafka
* **Database:** SQL Server 2022 utilizing Vector Search and Temporal Decay Stored Procedures
* **AI Engine:** Semantic Kernel and Ollama utilizing llama3.2:3b for chat generation and nomic-embed-text for vector embeddings
* **CI/CD:** GitHub Actions for Automated Docker Compose deployments to AWS

---

## System Workflow

1. **Data Ingestion:** A background service continuously aggregates the latest news from internet RSS feeds like CNBC, BBC and NYT, then publishes the raw payload to a Kafka topic.
2. **Vector Generation:** A consumer service listens to the Kafka topic. Upon receiving a new article, the service transmits the text payload to the local Ollama instance via the Ngrok tunnel to generate a 768-dimensional Vector Embedding.
3. **Persistence:** The vector embedding and corresponding news text are persisted in the SQL Server database.
4. **Retrieval and Generation:** When a query is initiated via the client interface, the API embeds the query, executes a Cosine Similarity search against the SQL database and applies a Temporal Decay algorithm to appropriately weight recent news. The retrieved context is then provided to the local LLM to generate the final response.

---

## Deployment Instructions

### 1. Edge Node Initialization for Ollama
Ensure Ollama is installed and provisioned with the necessary models:
```bash
ollama pull llama3.2:3b
ollama pull nomic-embed-text
```

Initialize an Ngrok tunnel to expose the Ollama service to the AWS infrastructure. The `--host-header` flag is required to bypass default local-origin security restrictions:
```bash
ngrok http --domain={your-domain.ngrok-free.dev} 11434 --host-header="localhost:11434"
```

### 2. Cloud Node Configuration
Clone the repository on the target server and configure the environment variables to establish the connection with the Edge Node:
```bash
echo "OLLAMA_ENDPOINT=https://{your-domain.ngrok-free.dev}/v1/" > .env
docker compose up -d --build
```

### 3. Database Initialization
Once the SQL Server container has completed its startup sequence, initialize the schema and the Vector Search stored procedures:
```bash
sudo docker exec -i rag-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C < 01_CreateSchema.sql
```

Restart the API service to establish the database connection and begin processing the Kafka stream:
```bash
docker restart rag-api
```

### 4. Continuous Deployment Pipeline
This repository is configured with a GitHub Actions workflow located at .github/workflows/deploy.yml. Any commits pushed to the main branch will automatically initiate an SSH session into the AWS EC2 instance and redeploy the latest Docker containers, utilizing configured health checks to ensure zero-downtime rollouts.
