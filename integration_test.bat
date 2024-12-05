@echo off
REM Test Register Endpoint
echo Testing Register Endpoint...
curl -X POST http://127.0.0.1:8080/register -H "Content-Type: application/json" -d "{\"username\": \"testuser\", \"password\": \"password\"}"
echo.

REM Test Login Endpoint
echo Testing Login Endpoint...
curl -X POST http://127.0.0.1:8080/login -H "Content-Type: application/json" -d "{\"username\": \"testuser\", \"password\": \"password\"}"
echo.

REM End of tests
echo Tests completed.
pause