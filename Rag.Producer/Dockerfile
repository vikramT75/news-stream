FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Rag.Producer.csproj", "./"]
RUN dotnet restore "Rag.Producer.csproj"
COPY . .
RUN dotnet publish "Rag.Producer.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Rag.Producer.dll"]
