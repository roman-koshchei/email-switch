# Email Switch

Self-hostable API for highly available email sending through pool of providers.

Main goal is to avoid getting downtime when email service you use goes down.
Like it happened with [Postmark](https://postmarkapp.com/), sometimes [Resend](https://resend.com/) and other providers.

Currently I plan to support only emails with single receiver, maybe later support for multiple receivers will come.

Most providers give you option to use SMTP server directly,
so if project doesn't implement your particular provider then use SMTP one.

|     |     |     |
| --- | --- | --- |

Previously it was a small module I have created for my e-commerce platform
(because I wanted to spend no money): [roman-koshchei.github.io/unator/switches/email](https://roman-koshchei.github.io/unator/switches/email).
