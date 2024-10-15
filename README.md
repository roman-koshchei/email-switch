![Banner](./assets/email-switch-preview.png)

# Email Switch

Self-hostable API for highly available email sending through pool of providers.

Table of content:

- [Introduction](#introduction)
- [Quick Start](#quick-start)
- [Providers](#providers)
- [Deployment](#deployment)

## Introduction

Main goal is to avoid getting downtime when email service you use goes down.

Like it happened with [Postmark](https://postmarkapp.com/blog/outbound-smtp-outage-on-september-15-2024), sometimes [Resend](https://resend.com/blog/incident-report-for-february-21-2024) and other providers.

Currently I plan to support only emails with single receiver (or small list), maybe later support for broadcasting (thousands of receivers) will come.

## Quick Start

Email Switch is configured through file or value inside of Environment variable.

If you want to use File then set `PROVIDERS_FILE` environment variable to configuration file path.

If you want to use Value from env variable then set `PROVIDERS_VALUE` environment variable with configuration file value.

Configuration is json with such structure:

```json
[
  {
    "id": "provider-1-id",
    "property-1-of-provider-1": "value-1",
    "property-2-of-provider-1": "value-2"
  },
  {
    "id": "provider-2-id",
    "property-1-of-provider-2": "value-1",
    "property-2-of-provider-2": "value-2"
  }
]
```

Supported providers and properties that they require can be found in [Providers section](#providers) or in code.

Example of configuration:

```json
[
  {
    "id": "resend",
    "token": "resend-api-token"
  },
  {
    "id": "smtp",
    "host": "smtp.example.com",
    "port": 456,
    "user": "user",
    "password": "password"
  },
  {
    "id": "sendgrid",
    "token": "send-grid-api-key"
  },
  {
    "id": "brevo",
    "token": "brevo-api-token"
  }
]
```

## Providers

Most providers give you option to use SMTP server directly,
so if the project doesn't implement your particular provider then use SMTP one.

| Service                                              | Status        |
| ---------------------------------------------------- | ------------- |
| SMTP                                                 | Seems to work |
| [Resend](https://resend.com/)                        | Seems to work |
| [Brevo](https://www.brevo.com/)                      | Seems to work |
| [SendGrid](https://sendgrid.com/)                    | Seems to work |
| [Postmark](https://postmarkapp.com/)                 | Planned       |
| [Mailchimp](https://mailchimp.com/)                  | Planned       |
| [Mailjet](https://www.mailjet.com/)                  | Planned       |
| [Amazon SES](https://aws.amazon.com/ru/ses/pricing/) | Planned       |

## Deployment

Docker brother, Docker. For now it's most viable option, because of the way people are deploying stuff.

And with Docker I can move whole service to Native AOT Compilation without worrying about cross compiling (it's not supported for native aot for now).

If there will be requests to have it compiled without docker into pure executable file, then we will solve it.

## Related Posts

- https://x.com/resend/status/1827303292915085742
- https://x.com/thdxr/status/1836625504306126991
- https://x.com/AvgDatabaseCEO/status/1836563526233592056

## History

Previously it was a small module I have created for my e-commerce platform
(because I wanted to spend no money): [roman-koshchei.github.io/unator/switches/email](https://roman-koshchei.github.io/unator/switches/email).
