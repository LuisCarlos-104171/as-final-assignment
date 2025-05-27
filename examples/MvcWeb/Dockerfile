# Use the official .NET runtime as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["examples/MvcWeb/MvcWeb.csproj", "examples/MvcWeb/"]
COPY ["core/Piranha/Piranha.csproj", "core/Piranha/"]
COPY ["core/Piranha.AspNetCore/Piranha.AspNetCore.csproj", "core/Piranha.AspNetCore/"]
COPY ["core/Piranha.AttributeBuilder/Piranha.AttributeBuilder.csproj", "core/Piranha.AttributeBuilder/"]
COPY ["core/Piranha.ImageSharp/Piranha.ImageSharp.csproj", "core/Piranha.ImageSharp/"]
COPY ["core/Piranha.Local.FileStorage/Piranha.Local.FileStorage.csproj", "core/Piranha.Local.FileStorage/"]
COPY ["core/Piranha.Manager/Piranha.Manager.csproj", "core/Piranha.Manager/"]
COPY ["core/Piranha.Manager.LocalAuth/Piranha.Manager.LocalAuth.csproj", "core/Piranha.Manager.LocalAuth/"]
COPY ["core/Piranha.Manager.TinyMCE/Piranha.Manager.TinyMCE.csproj", "core/Piranha.Manager.TinyMCE/"]
COPY ["data/Piranha.Data.EF.SQLite/Piranha.Data.EF.SQLite.csproj", "data/Piranha.Data.EF.SQLite/"]
COPY ["identity/Piranha.AspNetCore.Identity.SQLite/Piranha.AspNetCore.Identity.SQLite.csproj", "identity/Piranha.AspNetCore.Identity.SQLite/"]
COPY ["Directory.Build.props", "Directory.Build.targets", "./"]

RUN dotnet restore "examples/MvcWeb/MvcWeb.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/examples/MvcWeb"
RUN dotnet build "MvcWeb.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MvcWeb.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage/image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create directory for SQLite database
RUN mkdir -p /app/data

ENTRYPOINT ["dotnet", "MvcWeb.dll"]