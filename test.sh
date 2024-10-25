#!/bin/bash

curl -v -X POST http://localhost:3000/api/emails \
-H "Content-Type: application/json" \
-H "Authorization: Bearer a" \
-d '{
    "fromEmail": "alice@example.com",
    "to": ["bob@example.com", "carol@example.com"],
    "subject": "Hello!",
    "text": "This is a plain text message.",
    "html": "<p>This is an HTML message.</p>"
}'
