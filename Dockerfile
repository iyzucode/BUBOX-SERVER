FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project definition files and restore dependencies
COPY Bubox.Api/Bubox.Api.csproj Bubox.Api/
COPY Bubox.Application/Bubox.Application.csproj Bubox.Application/
COPY Bubox.Domain/Bubox.Domain.csproj Bubox.Domain/
COPY Bubox.Infrastructure/Bubox.Infrastructure.csproj Bubox.Infrastructure/
RUN dotnet restore Bubox.Api/Bubox.Api.csproj

# Copy source code and publish release build
COPY . .
WORKDIR /src/Bubox.Api
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Set default ASP.NET Core URL to listen on port 5073
ENV ASPNETCORE_URLS=http://+:5073
EXPOSE 5073

ENTRYPOINT ["dotnet", "Bubox.Api.dll"]
