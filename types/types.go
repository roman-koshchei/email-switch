package types

type Email struct {
	FromEmail string   `json:"fromEmail"`
	FromName  string   `json:"fromName"`
	To        []string `json:"to"`
	Subject   string   `json:"subject"`
	Text      string   `json:"text"`
	Html      string   `json:"html"`
}

func (email *Email) IsValid() bool {
	if email.FromEmail == "" || !isEmail(email.FromEmail) {
		return false
	}
	if len(email.To) == 0 {
		return false
	}
	if email.Subject == "" {
		return false
	}
	if email.Html == "" && email.Text == "" {
		return false
	}
	return true
}

func isEmail(str string) bool {
	index := -1
	for i, char := range str {
		if char == '@' {
			if index != -1 {
				return false
			}
			index = i
		}
	}

	return index > 0 && index != len(str)-1
}
