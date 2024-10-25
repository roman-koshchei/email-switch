# syntax=docker/dockerfile:1

FROM golang:1.23.2-alpine AS builder

WORKDIR /app

COPY ./go.mod ./go.sum ./
RUN go mod download

COPY ./ ./

# Build
RUN CGO_ENABLED=0 GOOS=linux go build -o /email-switch ./main.go

FROM scratch

COPY --from=builder /email-switch ./

EXPOSE 8080

# Run
ENTRYPOINT ["/email-switch"]