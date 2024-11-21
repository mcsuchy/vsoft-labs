# vsoft-labs

# DOCKER:
- budowanie obrazu: docker build --no-cache -t todo-image .

- tworzenie kontenera: docker create --name todo-counter todo-image

- uruchomienie kontenera: docker start todo-counter

- uruchomić image z odpowiednimi zmiennymi: docker run -e ASPNETCORE_ENVIRONMENT=Development -e  "ConnectionStrings__DefaultConnection=Server=tcp:lab-sql-02.database.windows.net,1433;Initial Catalog=lab-02-sql;Persist Security Info=False;User ID=labadmin;Password=Vision123$;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;" -p 5000:8080 todo-image

- uruchomić image z odpowiednimi zmiennymi: docker run -e ASPNETCORE_ENVIRONMENT=Development -e  "ConnectionStrings__DefaultConnection=Server=tcp:lab-sql-02.database.windows.net,1433;Initial Catalog=lab-02-sql;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Default;" -p 5000:8080 todo-image



#Azure container registry

az acr login -n tododockerregistry.azurecr.io
docker tag todo-image:latest tododockerregistry.azurecr.io/todo-image:v1
docker push tododockerregistry.azurecr.io/todo-image:v1

#Uruchomienie lokalne z bazą AAD Default
dotnet run --environment Development --ConnectionStrings:DefaultConnection="Server=tcp:lab-sql-02.database.windows.net,1433;Initial Catalog=lab-02-sql;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Default"

#dodawanie managed identity (Azure CLI)

export RESOURCE_GROUP="RG-lab02"
export LOCATION="PolandCentral"
export CLUSTER_NAME="todo-aks-lab2"
export SERVICE_ACCOUNT_NAMESPACE="default"
export SERVICE_ACCOUNT_NAME="workload-identity-sa"
export SUBSCRIPTION="$(az account show --query id --output tsv)"
export USER_ASSIGNED_IDENTITY_NAME="todo-managed-identity"
export FEDERATED_IDENTITY_CREDENTIAL_NAME="todo-fed-identity"

az identity create \
    --name "${USER_ASSIGNED_IDENTITY_NAME}" \
    --resource-group "${RESOURCE_GROUP}" \
    --location "${LOCATION}" \
    --subscription "${SUBSCRIPTION}"

#Aktywowanie  Application Routing dla klastra AKS

az aks approuting enable --resource-group RG-lab02 --name todo-aks-lab2