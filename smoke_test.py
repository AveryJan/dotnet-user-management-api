"""Smoke-test a running User Management API using only Python's standard library.
Start it first: dotnet run --urls http://localhost:5000
Then: python scripts/smoke_test.py
"""
import json
import urllib.error
import urllib.request

BASE = 'http://localhost:5000'


def request(method, path, data=None):
    body = json.dumps(data).encode() if data is not None else None
    req = urllib.request.Request(BASE + path, data=body, method=method)
    if body is not None:
        req.add_header('Content-Type', 'application/json')
    try:
        with urllib.request.urlopen(req, timeout=10) as response:
            raw = response.read()
            return response.status, json.loads(raw) if raw else None
    except urllib.error.HTTPError as error:
        raw = error.read()
        return error.code, json.loads(raw) if raw else None


def check(label, method, path, expected, data=None):
    status, result = request(method, path, data)
    if status != expected:
        raise AssertionError(f'{label}: expected HTTP {expected}, got {status}: {result}')
    print(f'PASS {label}: HTTP {status}')
    return result


if __name__ == '__main__':
    check('Service health', 'GET', '/', 200)
    check('List users', 'GET', '/api/users', 200)
    created = check('Create user', 'POST', '/api/users', 201, {'name': 'Test User', 'email': 'test.user@example.com'})
    identifier = created['id']
    try:
        check('Read user', 'GET', f'/api/users/{identifier}', 200)
        check('Duplicate email', 'POST', '/api/users', 409, {'name': 'Another User', 'email': 'TEST.USER@example.com'})
        check('Invalid email', 'POST', '/api/users', 400, {'name': 'Invalid User', 'email': 'not-an-email'})
        check('Missing name', 'POST', '/api/users', 400, {'name': '', 'email': 'new@example.com'})
        check('Update user', 'PUT', f'/api/users/{identifier}', 200, {'name': 'Updated User', 'email': 'updated@example.com'})
    finally:
        check('Delete user', 'DELETE', f'/api/users/{identifier}', 204)
    check('Deleted user not found', 'GET', f'/api/users/{identifier}', 404)
    print('All 10 API smoke tests passed.')
