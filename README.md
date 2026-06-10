# Жасыл Ел Казахстан

Браузерная стратегия про озеленение территории Казахстана. Проект запускается как один ASP.NET Core Web API сервер: backend отдаёт REST API, JWT-авторизацию и статический frontend из `wwwroot`.

## Стек

- ASP.NET Core Web API
- Entity Framework Core
- JWT авторизация
- SQL Server-ready конфигурация
- Dev-режим на EF InMemory, чтобы проект сразу открывался без установленного SQL Server
- HTML, CSS, JavaScript
- Phaser.js

## Запуск

```powershell
dotnet restore
dotnet run --project JasylEl.csproj
```

Откройте:

```text
http://localhost:5000
```

Swagger:

```text
http://localhost:5000/swagger
```

## База данных

По умолчанию включён режим:

```json
"Database": {
  "Provider": "InMemory"
}
```

Так проект открывается сразу на компьютере без SQL Server. Для SQL Server поменяйте `Provider` на `SqlServer` и используйте строку подключения `DefaultConnection` в `appsettings.json`.

## Структура

```text
JasylEl.csproj
Program.cs
AppDbContext.cs
Models.cs
DTOs.cs
Repositories.cs
Services.cs
Controllers.cs
wwwroot/
  index.html
  css/style.css
  js/app.js
```

## Реализовано

- регистрация, вход и JWT;
- профиль игрока: уровень, опыт, ресурсы, репутация, регион;
- регионы Казахстана с бонусами;
- карта Phaser.js 3000x3000 с перемещением мышью и zoom колесом;
- посадка деревьев с редкостью, стоимостью и стадиями роста;
- постройки с уровнями и улучшением;
- экологические события: пожар, засуха, мусор, болезни;
- викторины по биологии, географии Казахстана, экологии и Жасыл Ел;
- награды за правильные ответы;
- достижения и рейтинг игроков;
- seed-данные на русском языке без битой кодировки.
