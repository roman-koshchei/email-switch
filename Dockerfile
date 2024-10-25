# syntax=docker/dockerfile:1

FROM golang:1.23.2-alpine AS builder

# Set destination for COPY
WORKDIR /app

# Download Go modules
COPY go.mod go.sum ./
RUN go mod download

COPY *.go ./

# Build
RUN CGO_ENABLED=0 GOOS=linux go build -o /email-switch

FROM scratch

COPY --from=builder /app/email-switch .

EXPOSE 8080

# Run
ENTRYPOINT ["/email-switch"]