# Mobile Bill Management API (ASP.NET Core 10).
# Build from the repository root:  docker build -f src/MobileBill.Api/Dockerfile -t mobilebill-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Restore first so package downloads are cached between builds.
COPY global.json MobileBill.sln ./
COPY src/MobileBill.Domain/MobileBill.Domain.csproj src/MobileBill.Domain/
COPY src/MobileBill.Application/MobileBill.Application.csproj src/MobileBill.Application/
COPY src/MobileBill.Infrastructure/MobileBill.Infrastructure.csproj src/MobileBill.Infrastructure/
COPY src/MobileBill.Api/MobileBill.Api.csproj src/MobileBill.Api/
RUN dotnet restore src/MobileBill.Api/MobileBill.Api.csproj
COPY src/ src/
RUN dotnet publish src/MobileBill.Api/MobileBill.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# The PDF report needs real fonts; the Linux base image has none. Liberation Sans matches Arial's widths.
RUN apt-get update \
    && apt-get install -y --no-install-recommends fonts-liberation \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
# Folder for uploaded bill PDFs; mounted as a volume by docker-compose. Owned by the non-root app user.
RUN mkdir -p /data/bill-uploads && chown -R $APP_UID /data/bill-uploads
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    BillStorage__RootPath=/data/bill-uploads
EXPOSE 8080
ENTRYPOINT ["dotnet", "MobileBill.Api.dll"]
