package emails

import (
	"encoding/json"

	"github.com/gofiber/fiber/v2/log"
)

// type EmailSenderFactory struct {
// 	id     string
// 	action func(map[string]interface{}) EmailSender
// }

func Parse(jsonData []byte) []EmailSender {
	var objects []map[string]any

	err := json.Unmarshal(jsonData, &objects)
	if err != nil {
		log.Fatalf("Error parsing JSON: %v", err)
		return []EmailSender{}
	}

	providers := make([]EmailSender, 0)

	for _, object := range objects {
		id, ok := object["id"]
		if !ok {
			log.Warn("Id isn't present for one of providers, so it's skipped")
			continue
		}

		if id == "resend" {
			token := object["token"].(string)
			providers = append(providers, NewResend(token))
		} else if id == "test" {
			providers = append(providers, TestSender{})
		} else {
			log.Warnf("There is no factory for id: %s", id)
		}

	}

	return providers
}
