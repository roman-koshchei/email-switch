# cp ./.env ./src/Api/.env
# cp ./providers.json ./src/Api/providers.json

dotnet run --project ./EmailSwitch.csproj

# rm ./src/Api/providers.json