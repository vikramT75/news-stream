FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Rag.Api.csproj", "./"]
RUN dotnet restore "Rag.Api.csproj"
COPY . .
RUN dotnet publish "Rag.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Rag.Api.dll"]
