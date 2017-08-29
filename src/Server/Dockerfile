FROM microsoft/dotnet:latest AS build-env
WORKDIR /app
ADD *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out

FROM microsoft/dotnet:runtime
WORKDIR /app
COPY --from=build-env /app/out .

EXPOSE 10891
ENTRYPOINT ["dotnet", "Server.dll"]
