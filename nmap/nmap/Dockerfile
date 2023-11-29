FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

RUN apt-get update  \
    && apt-get install nmap -y
    
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["nmap/nmap.csproj", "nmap/"]
RUN dotnet restore "nmap/nmap.csproj"
COPY . .
WORKDIR "/src/nmap"
RUN dotnet build "nmap.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "nmap.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "nmap.dll"]
