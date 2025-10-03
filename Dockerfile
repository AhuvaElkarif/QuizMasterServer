FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet build -c Release --no-restore
RUN dotnet publish -c Release -o /app --no-build

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# ליצור תיקיית keys ל־Data Protection
RUN mkdir -p /app/keys

EXPOSE 10000
ENV ASPNETCORE_URLS=http://+:10000

ENTRYPOINT ["dotnet", "QuizMasterServer.dll"]
