cp ./.env ./src/Api/.env
cp ./providers.json ./src/Api/providers.json

dotnet run --project ./src/Api/Api.csproj

rm ./src/Api/providers.json