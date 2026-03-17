# School Schedule API (Python/FastAPI)

REST API for school schedule management with full CRUD and automatic seed data.

## What is included
- Full CRUD for:
  - `subjects`
  - `classrooms`
  - `teachers`
  - `classes`
  - `workloads`
  - `lessons`
  - `users`
- Automatic DB initialization on startup.
- Automatic seed insertion (runs once when DB is empty).
- Relation checks for FK fields and conflict handling (`409`).

## Quick start
1. Install dependencies:
```bash
pip install -r requirements.txt
```

2. Start API:
```bash
py main.py
```

3. Open docs:
- Swagger: `http://localhost:8000/docs`
- ReDoc: `http://localhost:8000/redoc`

## Database
By default the API uses local SQLite file:
```text
sqlite:///./school_schedule.db
```

To use another DB (for example SQL Server), set environment variable:
```text
DATABASE_URL=<your_sqlalchemy_url>
```

## Main endpoints
- `GET /health`
- `GET/POST /subjects`
- `GET/PATCH/DELETE /subjects/{id}`
- `GET/POST /classrooms`
- `GET/PATCH/DELETE /classrooms/{id}`
- `GET/POST /teachers`
- `GET/PATCH/DELETE /teachers/{id}`
- `GET/POST /classes`
- `GET/PATCH/DELETE /classes/{id}`
- `GET/POST /workloads`
- `GET/PATCH/DELETE /workloads/{id}`
- `GET/POST /lessons`
- `GET/PATCH/DELETE /lessons/{id}`
- `GET/POST /users`
- `GET/PATCH/DELETE /users/{id}`

## Example request
```bash
curl -X POST http://localhost:8000/subjects \
  -H "Content-Type: application/json" \
  -d "{\"Name\":\"Biology\"}"
```
