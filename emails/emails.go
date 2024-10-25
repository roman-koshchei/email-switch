package emails

import (
	"fmt"
	"strings"

	"github.com/roman-koshchei/email-switch/types"

	"github.com/gofiber/fiber/v2"
	"github.com/gofiber/fiber/v2/log"
)

type EmailSender interface {
	Send(*types.Email) bool
}

const RESEND_URL = "https://api.resend.com/emails"

type Resend struct {
	token string
}

func NewResend(token string) *Resend {
	return &Resend{token: token}
}

func (sender *Resend) Send(email *types.Email) bool {
	agent := fiber.Post(RESEND_URL)
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

	return status < 300 && status >= 200
}

type TestSender struct{}

func (TestSender) Send(email *types.Email) bool {
	log.Info("From Email: " + email.FromEmail)
	log.Info("From Name: " + email.FromName)
	log.Info("To: " + strings.Join(email.To, ", "))
	log.Info("Subject: " + email.Subject)

	return true
}
