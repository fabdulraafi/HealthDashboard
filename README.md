Distributed Microservices Monitoring Dashboard

A real-time health monitoring platform for .NET-based microservice architectures.

---

Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)

---

Step 1 — Start Docker Desktop

Open Docker Desktop from your Applications folder and wait for it to fully load (whale icon appears in the menu bar).

---

Step 2 — Start Seq

Open a terminal and run:
```bash
docker run -d --name seq -e ACCEPT_EULA=Y -e SEQ_FIRSTRUN_ADMINPASSWORD=Admin1234 -p 5341:5341 -p 8081:80 datalust/seq
```
If you have run this before, just run 'docker start seq' instead.

---

Step 3 — Run the Project

```bash
dotnet restore Aggregator.csproj
dotnet run
```

---

Step 4 — Open the Dashboard

| Page | URL |
|---|---|
| Dashboard | http://localhost:5154/dashboard |
| Health History | http://localhost:5154/history |
| Seq Logs | http://localhost:8081 |

---

Testing

- Visit http://localhost:5154/break to simulate a service failure
- Visit http://localhost:5154/fix to recover it