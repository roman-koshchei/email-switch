package emails

import (
	"fmt"
	"net"
	"net/smtp"
	"strings"

	"github.com/roman-koshchei/email-switch/types"

	"github.com/gofiber/fiber/v2"
	"github.com/gofiber/fiber/v2/log"
)

type EmailSender interface {
	Send(*types.Email) bool
}

func statusSuccessful(status int) bool {
	return status < 300 && status >= 200
}

type EmailSwitch struct {
	senders []EmailSender
}

func NewEmailSwitch(senders []EmailSender) *EmailSwitch {
	return &EmailSwitch{senders: senders}
}

func (sender *EmailSwitch) Send(email *types.Email) bool {
	// bruh, it's just a loop?
	// yeah, for now. later it will be "smart" loop
	for _, sender := range sender.senders {
		if sender.Send(email) {
			return true
		}
	}
	return false
}

const ResendUrl = "https://api.resend.com/emails"

type Resend struct {
	token string
}

func NewResend(token string) *Resend {
	return &Resend{token: token}
}

func (sender Resend) Send(email *types.Email) bool {
	agent := fiber.Post(ResendUrl)
	agent.JSON(fiber.Map{
		"from":    fmt.Sprintf("%s <%s>", email.FromName, email.FromEmail),
		"to":      email.To,
		"subject": email.Subject,
		"text":    email.Text,
		"html":    email.Html,
	})
	agent.Add("Authorization", "Bearer "+sender.token)

	status, _, errs := agent.Bytes()
	if len(errs) > 0 {
		return false
	}

	return statusSuccessful(status)
}

type TestSender struct{}

func (TestSender) Send(email *types.Email) bool {
	log.Info("From Email: " + email.FromEmail)
	log.Info("From Name: " + email.FromName)
	log.Info("To: " + strings.Join(email.To, ", "))
	log.Info("Subject: " + email.Subject)

	return true
}

const BrevoUrl = "https://api.brevo.com/v3/smtp/email"

type Brevo struct {
	token string
}

func NewBrevo(token string) *Brevo {
	return &Brevo{token: token}
}

func (sender Brevo) Send(email *types.Email) bool {
	agent := fiber.Post(BrevoUrl)

	toResult := make([]map[string]any, len(email.To))
	for i, toEmail := range email.To {
		toResult[i] = fiber.Map{
			"email": toEmail,
		}
	}

	agent.JSON(fiber.Map{
		"sender": fiber.Map{
			"name":  email.FromName,
			"email": email.FromEmail,
		},
		"to":          toResult,
		"subject":     email.Subject,
		"htmlContent": email.Html,
		"textContent": email.Text,
	})
	agent.Add("api-key", sender.token)

	status, _, errs := agent.Bytes()
	if len(errs) > 0 {
		return false
	}

	return statusSuccessful(status)
}

const SendgridUrl = "https://api.sendgrid.com/v3/mail/send"

type SendGrid struct {
	token string
}

func NewSendGrid(token string) *SendGrid {
	return &SendGrid{token: token}
}

type SendGridContent struct {
	Type  string `json:"type"`
	Value string `json:"value"`
}

type SendGridPerson struct {
	Email string `json:"email"`
	Name  string `json:"name"`
}

type SendGridPersonalizationsTo struct {
	Email string `json:"email"`
}

type SendGridPersonalizations struct {
	To []SendGridPersonalizationsTo `json:"to"`
}

type SendGridPayload struct {
	Personalizations []SendGridPersonalizations `json:"personalizations"`

	From SendGridPerson `json:"from"`

	ReplyTo SendGridPerson `json:"reply_to"`

	Subject string `json:"subject"`

	Content []SendGridContent `json:"content"`
}

func (sender SendGrid) Send(email *types.Email) bool {
	agent := fiber.Post(SendgridUrl)

	from := SendGridPerson{
		Email: email.FromEmail,
		Name:  email.FromName,
	}

	toResult := make([]SendGridPersonalizationsTo, len(email.To))
	for i, toEmail := range email.To {
		toResult[i] = SendGridPersonalizationsTo{
			Email: toEmail,
		}
	}

	payload := SendGridPayload{
		Personalizations: []SendGridPersonalizations{{
			To: toResult,
		}},
		From:    from,
		ReplyTo: from,
		Subject: email.Subject,
		Content: []SendGridContent{
			{Type: "text/plain", Value: email.Text},
			{Type: "text/html", Value: email.Html},
		},
	}
	agent.JSON(payload)
	agent.Add("Authorization", "Bearer "+sender.token)

	status, _, errs := agent.Bytes()
	if len(errs) > 0 {
		return false
	}

	return statusSuccessful(status)
}

type Smtp struct {
	Host     string
	Port     int
	User     string
	Password string
}

func NewSmtp(host string, port int, user, password string) *Smtp {
	return &Smtp{
		Host:     host,
		Port:     port,
		User:     user,
		Password: password,
	}
}

func (sender *Smtp) Send(email *types.Email) bool {
	var body string
	if len(email.Html) > 0 {
		body = email.Html
	} else {
		body = email.Text
	}

	auth := smtp.PlainAuth("", sender.User, sender.Password, sender.Host)
	addr := net.JoinHostPort(sender.Host, fmt.Sprint(sender.Port))

	err := smtp.SendMail(addr, auth, email.FromEmail, email.To, []byte(body))
	return err == nil
}
