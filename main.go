package main

import (
	"crypto/sha256"
	"encoding/base64"
	"fmt"
	"github.com/golang-jwt/jwt/v5"
	"os"
	"strings"
	"time"

	"github.com/roman-koshchei/email-switch/emails"
	"github.com/roman-koshchei/email-switch/types"

	"github.com/gofiber/fiber/v2"
	"github.com/gofiber/fiber/v2/log"
)

var RootApiKey string
var Providers []emails.EmailSender
var QStashCurrentSigningKey string
var QStashNextSigningKey string
var QStash bool = false

func init() {
	envFromFile("./.env")

	RootApiKey = os.Getenv("ROOT_API_KEY")

	var providersData []byte
	providersValue := os.Getenv("PROVIDERS_VALUE")
	if len(providersValue) > 0 {
		providersData = []byte(providersValue)
	} else {
		providersFile := os.Getenv("PROVIDERS_FILE")
		if len(providersFile) == 0 {
			providersFile = "./providers.json"
		}

		data, err := os.ReadFile(providersFile)
		if err != nil {
			fmt.Println("Providers file can't be accessed or doesn't exist")
			panic(err)
		}

		providersData = data
	}
	Providers = emails.Parse(providersData)

	qstashEnv := os.Getenv("QSTASH")
	if "true" == qstashEnv || "1" == qstashEnv {
		QStash = true
		QStashCurrentSigningKey = os.Getenv("QSTASH_CURRENT_SIGNING_KEY")
		QStashNextSigningKey = os.Getenv("QSTASH_NEXT_SIGNING_KEY")
	}

}

func main() {
	sender := emails.NewEmailSwitch(Providers)

	app := fiber.New()

	app.Get("/", func(c *fiber.Ctx) error {
		return c.SendString("Email Switch")
	})

	api := app.Group("/api", Authorization)
	api.Post("/emails", func(c *fiber.Ctx) error {

		input := new(types.Email)
		if err := c.BodyParser(input); err != nil || !input.IsValid() {
			return c.SendStatus(fiber.StatusBadRequest)
		}

		if sender.Send(input) {
			return c.SendStatus(fiber.StatusOK)
		}

		return c.SendStatus(fiber.StatusInternalServerError)
	})

	if QStash {
		app.Post("/qstash", func(c *fiber.Ctx) error {
			signature := c.Get("Upstash-Signature")

			body := c.BodyRaw()

			legit := verifyRequestWithKey(QStashCurrentSigningKey, signature, body)
			if !legit {
				legit = verifyRequestWithKey(QStashNextSigningKey, signature, body)
			}
			if !legit {
				return c.Status(fiber.StatusBadRequest).SendString("Signature isn't legit")
			}

			input := new(types.Email)
			if err := c.BodyParser(input); err != nil || !input.IsValid() {
				return c.Status(fiber.StatusBadRequest).SendString("Body has wrong shape")
			}

			if sender.Send(input) {
				return c.SendStatus(fiber.StatusOK)
			}

			return c.SendStatus(fiber.StatusInternalServerError)
		})
	}

	log.Fatal(app.Listen(":8080"))
}

func verifyRequestWithKey(key string, token string, body []byte) bool {
	signingKey := []byte(key)

	claims := jwt.MapClaims{}
	parsedToken, err := jwt.ParseWithClaims(token, claims,
		func(_ *jwt.Token) (interface{}, error) {
			return signingKey, nil
		},
		jwt.WithValidMethods([]string{jwt.SigningMethodHS256.Alg()}),
		jwt.WithIssuer("Upstash"),
		jwt.WithLeeway(time.Second),
		jwt.WithIssuedAt(),
	)
	if err != nil || !parsedToken.Valid {
		return false
	}

	// TODO: verify url

	jwtBodyHash, ok := claims["body"].(string)
	if !ok {
		return false
	}
	jwtBodyHash = strings.TrimRight(jwtBodyHash, "=")

	hashedBody := sha256.Sum256(body)
	base64Hash := strings.TrimRight(base64.URLEncoding.EncodeToString(hashedBody[:]), "=")
	if jwtBodyHash != base64Hash {
		return false
	}

	return true
}

func envFromFile(path string) {
	data, err := os.ReadFile(path)
	if err != nil {
		log.Info("Env file isn't found")
		return
	}

	log.Info("Reading env variables from " + path)

	content := string(data)
	lines := strings.Split(content, "\n")
	for _, line := range lines {
		key, value, ok := strings.Cut(line, "=")
		if ok && len(key) > 0 {
			os.Setenv(key, value)
		}
	}
}

func Authorization(c *fiber.Ctx) error {
	authHeader := c.Get("Authorization")
	parts := strings.Split(authHeader, " ")

	if len(parts) == 2 &&
		parts[0] == "Bearer" &&
		parts[1] == RootApiKey {

		return c.Next()
	}
	return c.SendStatus(fiber.StatusUnauthorized)
}
