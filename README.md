# User Management API — .NET 8

A small REST API built with ASP.NET Core Minimal APIs. It implements user CRUD, validation, duplicate-email protection, centralized error handling, and custom HTTP request logging middleware.

## Requirements

- .NET 8 SDK ([download](https://dotnet.microsoft.com/download/dotnet/8.0))

## Run locally

```bash
dotnet restore
dotnet run --urls http://localhost:5000
```

The API uses an in-memory store, so user records are lost when the server restarts. It is an educational demo, **not** a production identity/authentication service.

## Endpoints

| Method | Route | Purpose | Success status |
|---|---|---|---|
| GET | `/api/users` | List users | 200 |
| GET | `/api/users/{id}` | Retrieve user | 200, or 404 |
| POST | `/api/users` | Create user | 201, or 400/409 |
| PUT | `/api/users/{id}` | Update user | 200, or 400/404/409 |
| DELETE | `/api/users/{id}` | Remove user | 204, or 404 |

### Create a user

```bash
curl -i -X POST http://localhost:5000/api/users \
  -H 'Content-Type: application/json' \
  -d '{"name":"Alex Rivera","email":"alex@example.com"}'
```

Save the `id` returned in the response. Then use that ID in GET, PUT, and DELETE:

```bash
curl http://localhost:5000/api/users
curl http://localhost:5000/api/users/YOUR-USER-ID
curl -i -X PUT http://localhost:5000/api/users/YOUR-USER-ID \
  -H 'Content-Type: application/json' \
  -d '{"name":"Alex R.","email":"alex.r@example.com"}'
curl -i -X DELETE http://localhost:5000/api/users/YOUR-USER-ID
```

## Validation and errors

- Name is required and must be between 2 and 100 characters.
- Email is required, must be correctly formatted, and must not exceed 254 characters.
- Email addresses must be unique (case-insensitive).
- Invalid input returns HTTP 400 with validation details.
- Duplicate email returns HTTP 409; missing ID returns HTTP 404.
- Concurrent writes are synchronized with a lock to prevent duplicate records.

## Middleware

- `UseExceptionHandler()` handles unexpected errors without leaking internal exception details.
- Custom `app.Use(...)` middleware logs request method, path, status code, and elapsed time.

## Copilot debugging evidence (assignment requirement)

**Not yet verified:** The assignment awards 5 points for using GitHub Copilot to debug the API. Do **not** claim you did so unless true. If you used Copilot previously, add a short, factual note here describing your real debugging session, the issue fixed, and the resulting test. If not, open the project in VS Code with GitHub Copilot and ask it to review validation and error handling, reproduce a real issue, make and verify a fix, then document what happened. For example, you can ask Copilot to suggest edge cases for duplicate emails and invalid payloads. Record the actual changes and outcomes, not imagined ones.

## Grading rubric self-check

- [ ] Repository published **publicly** on GitHub
- [x] GET, POST, PUT, DELETE routes included
- [ ] Copilot debugging completed and **truthfully documented**
- [x] Request validation and duplicate-email protection included
- [x] Custom request-logging middleware included

## Limitations

- No database persistence or user authentication; add them before production use.
- This sample has not been compiled or smoke-tested in the authoring environment. Run `dotnet build` and the curl examples before submitting.
