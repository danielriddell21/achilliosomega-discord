FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["AchilliosOmega.Main/AchilliosOmega.Main.csproj", "AchilliosOmega.Main/"]
COPY ["AchilliosOmega.Discord/AchilliosOmega.Discord.csproj", "AchilliosOmega.Discord/"]
RUN dotnet nuget add source https://www.myget.org/F/discord-net/api/v3/index.json -n discord-net
RUN dotnet restore "AchilliosOmega.Main/AchilliosOmega.Main.csproj"
COPY . .
WORKDIR "/src/AchilliosOmega.Main"
RUN dotnet build "AchilliosOmega.Main.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AchilliosOmega.Main.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AchilliosOmega.Main.dll"]