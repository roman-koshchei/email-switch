![Banner](./assets/email-switch-preview.png)

# Email Switch

Self-hostable API for highly available email sending through pool of providers.

Main goal is to avoid getting downtime when email service you use goes down.
Like it happened with [Postmark](https://postmarkapp.com/), sometimes [Resend](https://resend.com/) and other providers.

Currently I plan to support only emails with single receiver, maybe later support for multiple receivers will come.

Most providers give you option to use SMTP server directly,
so if project doesn't implement your particular provider then use SMTP one.

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

Previously it was a small module I have created for my e-commerce platform
(because I wanted to spend no money): [roman-koshchei.github.io/unator/switches/email](https://roman-koshchei.github.io/unator/switches/email).

## Related Posts

- https://x.com/resend/status/1827303292915085742
- https://x.com/thdxr/status/1836625504306126991
- https://x.com/AvgDatabaseCEO/status/1836563526233592056

## Deployment

Docker brother, Docker. For now it's most viable option.
You can run it as systemd services as well and soon I may make a NixOS module for it.
