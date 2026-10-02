# Global News RAG

A cutting-edge Hybrid Cloud application that streams live global news into a Kafka event bus, embeds the articles using a local edge GPU and stores them in SQL Server for Semantic Search with Temporal Decay. 

When you ask a question, the AI retrieves the most relevant and recent news articles to construct an accurate, up-to-date answer.

## Architecture (Hybrid Cloud / Edge AI)

This project leverages a hybrid architecture to save cloud costs while maintaining global availability:
- **Cloud (AWS EC2):** Hosts the Web UI, API, Kafka Cluster and SQL Server.
- **Edge (Local PC):** Runs the heavy AI models (Ollama) locally via GPU, connected to the cloud securely via an Ngrok reverse tunnel.

### Tech Stack
* **Frontend:** Vanilla HTML/JS served via **Nginx**
* **Backend:** **.NET 8** ASP.NET Core API
* **Streaming:** **Confluent Kafka** (Event-driven ingestion)
* **Database:** **SQL Server 2022** (Vector Search + Temporal Decay Stored Procedures)
* **AI Engine:** **Semantic Kernel** + **Ollama** (`llama3.2:3b` for chat, `nomic-embed-text` for embeddings)
* **CI/CD:** **GitHub Actions** (Automated Docker Compose deployment to AWS)

---

## How It Works

1. **The Producer (`Rag.Producer`):** A background service constantly fetches the latest news from internet RSS feeds (CNBC, BBC and NYT) and publishes the raw text to a Kafka topic.
2. **The Consumer (`Rag.Api/IngestionWorker`):** Listens to the Kafka topic. When a new article arrives, it beams the text down to your local Ollama instance via Ngrok to generate a 768-dimensional Vector Embedding.
3. **The Database:** The embedding and the news text are stored in SQL Server.
4. **The UI:** When a user asks a question on the website, the API embeds the question, runs a **Cosine Similarity** search against the SQL database and applies a **Temporal Decay** algorithm so older news is penalized. The best articles are sent to the local LLM to generate the final response!

---

## Setup Instructions

### 1. Start the Local AI (Ollama)
Ensure you have [Ollama](https://ollama.com/) installed with the necessary models:
```bash
ollama pull llama3.2:3b
ollama pull nomic-embed-text
```

Start an Ngrok tunnel to expose Ollama to your AWS server. (The `--host-header` flag is required to bypass Ollama's local-only security):
```bash
ngrok http --domain=your-custom-domain.ngrok-free.dev 11434 --host-header="localhost:11434"
```

### 2. AWS Server Setup
Clone the repository on your server and set the Ngrok URL so the cloud can talk to your GPU:
```bash
echo "OLLAMA_ENDPOINT=https://{your-domain.ngrok-free.dev}/v1/" > .env
docker compose up -d --build
```

### 3. Initialize the SQL Database
Once the SQL Server container is running, initialize the schema and the Vector Search stored procedures:
```bash
sudo docker exec -i rag-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C < 01_CreateSchema.sql
```

Restart the API to connect to the new database and ingest the Kafka stream:
```bash
docker restart rag-api
```

### 4. Continuous Deployment
This repository includes a GitHub Action (`.github/workflows/deploy.yml`). Any push to the `main` branch will automatically SSH into your AWS EC2 instance and redeploy the latest Docker containers smoothly using health checks!
