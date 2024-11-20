# vsoft-labs

# DOCKER:
- budowanie obrazu: docker build --no-cache -t todo-image .

- tworzenie kontenera: docker create --name todo-counter todo-image

- uruchomienie kontenera: docker start todo-counter

- uruchomić image z odpowiednimi zmiennymi: docker run -e ASPNETCORE_ENVIRONMENT=Development -e  "ConnectionStrings__DefaultConnection=Server=tcp:lab-sql-02.database.windows.net,1433;Initial Catalog=lab-02-sql;Persist Security Info=False;User ID=labadmin;Password=Vision123$;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;" -p 5000:8080 todo-image