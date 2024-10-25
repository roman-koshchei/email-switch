package main

import (
	"fmt"
	"os"
	"strings"

	"email-switch/emails"
	"email-switch/types"

	"github.com/gofiber/fiber/v2"
	"github.com/gofiber/fiber/v2/log"
)

var ROOT_API_KEY string
var PROVIDERS_FILE string

func init() {
	envFromFile("./.env")

	ROOT_API_KEY = os.Getenv("ROOT_API_KEY")

	PROVIDERS_FILE = os.Getenv("PROVIDERS_FILE")
	if len(PROVIDERS_FILE) == 0 {
		PROVIDERS_FILE = "./providers.json"
	}
}

func main() {
	providersFileDate, err := os.ReadFile(PROVIDERS_FILE)
	if err != nil {
		fmt.Println("Error reading providers file:", err)
		return
	}

	providers := emails.Parse(providersFileDate)

	app := fiber.New()

	app.Get("/", func(c *fiber.Ctx) error {
		return c.SendString("Email Switch")
	})

	api := app.Group("/api", Authorization)

	api.Post("/emails", func(c *fiber.Ctx) error {

		input := new(types.Email)
		if err := c.BodyParser(input); err != nil {
			return c.SendStatus(fiber.StatusBadRequest)
		}

		if !input.IsValid() {
			return c.SendStatus(fiber.StatusBadRequest)
		}

		// bruh, it's just a loop?
		// yeah, for now. later it will be "smart" loop
		for _, provider := range providers {
			if provider.Send(input) {
				return c.SendStatus(fiber.StatusOK)
			}
		}

		return c.SendStatus(fiber.StatusInternalServerError)
	})

	log.Fatal(app.Listen(":3000"))
}

func envFromFile(path string) {
	data, err := os.ReadFile(path)
	if err != nil {
		log.Info("Env file isn't found")
		return
	}

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
		parts[1] == ROOT_API_KEY {

		return c.Next()
	}
	return c.SendStatus(fiber.StatusUnauthorized)
}
