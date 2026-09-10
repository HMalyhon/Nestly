# syntax=docker/dockerfile:1

# The API and the seeder ship as one image. They share every assembly and the 90 MB model, so two
# images would carry the same weights twice for the sake of one differing entrypoint.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first, so editing a .cs file does not re-resolve every package. Nestly.slnx comes
# along because the model fetcher locates the repository root by looking for it.
# .editorconfig comes along because it is where the analyser severities live. Without it the
# build inside the image runs StyleCop at its defaults, and a repo that compiles clean on a
# developer's machine fails on rules nobody there has ever seen.
COPY global.json Directory.Build.props Directory.Packages.props Nestly.slnx .editorconfig ./
COPY src/Nestly.Domain/Nestly.Domain.csproj src/Nestly.Domain/
COPY src/Nestly.Search/Nestly.Search.csproj src/Nestly.Search/
COPY src/Nestly.Api/Nestly.Api.csproj src/Nestly.Api/
COPY src/Nestly.Seeder/Nestly.Seeder.csproj src/Nestly.Seeder/
COPY tools/Nestly.ModelFetcher/Nestly.ModelFetcher.csproj tools/Nestly.ModelFetcher/

RUN dotnet restore src/Nestly.Api/Nestly.Api.csproj \
 && dotnet restore src/Nestly.Seeder/Nestly.Seeder.csproj \
 && dotnet restore tools/Nestly.ModelFetcher/Nestly.ModelFetcher.csproj

COPY src/ src/
COPY tools/ tools/

# Separate directories because both projects publish an appsettings.json, and one output folder
# would leave whichever was published second overwriting the other's configuration.
RUN dotnet publish src/Nestly.Api/Nestly.Api.csproj -c Release -o /app/api --no-restore \
 && dotnet publish src/Nestly.Seeder/Nestly.Seeder.csproj -c Release -o /app/seeder --no-restore

# Downloaded here rather than committed: 90 MB of weights in git is a repository nobody wants to
# clone. The fetcher checks each file against the SHA-256 recorded beside its URL, so the vectors
# this image produces are the ones the index was built from and not merely the right shape.
RUN dotnet run --project tools/Nestly.ModelFetcher -c Release --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# libgomp: onnxruntime's native library links against it and the runtime image does not carry it.
# curl: the healthcheck below, which is what the seeder and the web tier wait on.
RUN apt-get update \
 && apt-get install -y --no-install-recommends libgomp1 curl \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/api /app/api
COPY --from=build /app/seeder /app/seeder
COPY --from=build /src/models /models
COPY data/listings.csv.gz /data/listings.csv.gz

# Absolute paths, so both entrypoints read one copy. Left relative they resolve against whichever
# binary is running, and the image would need the model beside each of them.
ENV Embedding__ModelDirectory=/models \
    Seeder__DataPath=/data/listings.csv.gz

EXPOSE 8080

# The app directory, as a .NET image normally has it: both entrypoints then find their own
# appsettings.json whether or not they pin a content root themselves.
WORKDIR /app/api

# The seeder service overrides this; see docker-compose.yml.
ENTRYPOINT ["dotnet", "Nestly.Api.dll"]
