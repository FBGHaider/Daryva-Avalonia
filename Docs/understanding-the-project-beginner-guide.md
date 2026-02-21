# Understanding Daryva: Code & Structure (Beginner Guide)

This guide explains what Daryva is, how the code is organized, and how you would think about building something like it from the beginning—even if you are new to coding.

---

## Part 1: What is Daryva?

**Daryva** is a **property and tenant management** app. In plain English:

- You manage **houses** (properties).
- Each house has **tenants** (people who rent).
- You track **rent**, **payments**, **deposits**, **expenses**, and **documents**.
- You can send **notifications** (e.g. email reminders).

So the “product” is: a tool for landlords or property managers to keep everything in one place.

---

## Part 2: The Big Picture (Three Main Pieces)

Think of the app as **three layers** that work together:

```
  ┌─────────────────────────────────────────────────────────┐
  │  YOU (the user)                                          │
  └─────────────────────────────────────────────────────────┘
                              │
                              ▼
  ┌─────────────────────────────────────────────────────────┐
  │  1. DESKTOP APP (Daryva.UI)                              │
  │     What you see: windows, buttons, lists, forms.         │
  │     Runs on your computer (Windows, Mac, Linux).         │
  └─────────────────────────────────────────────────────────┘
                              │
                              │  talks to (over the internet or local network)
                              ▼
  ┌─────────────────────────────────────────────────────────┐
  │  2. API / BACKEND (Daryva.Api)                           │
  │     The “brain”: checks who you are, runs the rules,     │
  │     reads and saves data. Runs on a server (or your PC).  │
  └─────────────────────────────────────────────────────────┘
                              │
                              │  reads and writes
                              ▼
  ┌─────────────────────────────────────────────────────────┐
  │  3. DATABASE                                             │
  │     Where data is stored: houses, tenants, payments,     │
  │     etc. (PostgreSQL in production, SQLite for local).   │
  └─────────────────────────────────────────────────────────┘
```

- **Desktop app**: the interface you click and type in.
- **API**: the server program that does the real work (security, business logic, data).
- **Database**: the place where all the data is saved.

So: **you use the app → the app asks the API → the API uses the database.**  
If you were to start from zero, you would learn these three ideas first.

---

## Part 3: What’s in the Repo (Folders & Projects)

The solution (`.sln` file) is like a “container” that holds several **projects**. Each project is a piece of the system.

### Root level

- **`Daryva-Avalonia.sln`** – The solution file. Opening this in Visual Studio or Rider opens the whole project.
- **`src/`** – All the main source code lives under here.
- **`Tests/`** – Automated tests (to check that code still works).
- **`Docs/`** – Documentation (like this file).
- **`Scripts/`** – Helper scripts (e.g. start API + UI for development).
- **`docker-compose.yml`** – Runs a local PostgreSQL database in a “container” for development.

### Under `src/` – the four main projects

| Project        | What it is in simple terms |
|----------------|----------------------------|
| **Daryva.UI**  | The **desktop application** you see and click. Built with **Avalonia** (cross‑platform UI). |
| **Daryva.Api** | The **backend/API**: web server that handles login, houses, tenants, payments, etc. |
| **Daryva.Core**| Shared **core** code used by more than one project (if any). |
| **Daryva.Data**| **Data** definitions and sometimes **migrations** for the older SQLite/local setup. |

Most of the time you will work in **Daryva.UI** (screens and behaviour of the app) or **Daryva.Api** (endpoints and business logic).

---

## Part 4: Inside the Desktop App (Daryva.UI)

The UI is built with **Avalonia** and follows a pattern called **MVVM**.

### What is MVVM?

- **M**odel – the **data** (e.g. a house has a name, address, number of rooms).
- **V**iew – the **screen** (the actual window, buttons, text boxes).
- **V**iew**M**odel – the **logic** that connects the two: it holds the data and commands that the view uses.

So: **View** shows things and sends user actions to the **ViewModel**; **ViewModel** uses **Models** and talks to the **API** or local services.

### Main folders inside `src/Daryva.UI/`

| Folder      | Purpose |
|-------------|--------|
| **MVVM/**   | Most of the “logic” and structure of the app. |
| **Services/** | Code that talks to the API, or to local SQLite, or does things like export reports. |
| **Themes/** | Colours, fonts, styles (dark/light, etc.). |
| **Assets/** | Images, icons. |
| **App.axaml** / **Program.cs** | How the app starts (Avalonia + Velopack for updates). |

### Inside `MVVM/`

| Folder       | Purpose |
|--------------|--------|
| **Models/**  | Data shapes: e.g. `House`, `Tenant`, `Payment`. Simple classes with properties. |
| **ViewModels/** | One ViewModel per screen: e.g. `HousesViewModel` for the Houses screen. They load data, handle buttons, call services. |
| **Views/**   | The actual UI layout: `.axaml` files (like XAML/HTML for the screen) and sometimes code‑behind. |
| **Commands/** | Reusable “actions” (e.g. when you click “Save” or “Refresh”). |

**Example flow for “Houses” screen:**

1. User opens the **Houses** screen.
2. The **View** is something like `HousesView.axaml` (the layout).
3. The **ViewModel** is `HousesViewModel.cs`: it calls a **Service** to get the list of houses (from the API).
4. The list is stored in a property (e.g. `Houses`) that the View is “bound” to, so the table updates automatically when data arrives.

So when you change what the Houses screen *does*, you usually edit **HousesViewModel** and/or the **House**-related **Service**; when you change how it *looks*, you edit **HousesView.axaml**.

---

## Part 5: Inside the API (Daryva.Api)

The API is a **web server** that exposes **endpoints** (URLs) that the desktop app calls. It’s built with **ASP.NET Core**.

### Main folders inside `src/Daryva.Api/`

| Folder          | Purpose |
|-----------------|--------|
| **Controllers/**| Each controller handles one area: e.g. `HousesController` for “/api/houses”, `PaymentsController` for “/api/payments”. The controller receives the HTTP request and returns data or status. |
| **Services/**   | The real **business logic**: e.g. `HouseService` knows how to get houses, add one, and compute “active tenants” and “total monthly rent”. Controllers call services; services use the database. |
| **Domain/**     | **Entity** classes that match the database: e.g. `House`, `Tenant`, `Tenancy`, `RentPayment`. |
| **Data/**       | **DbContext**: the link between the app and the database (Entity Framework). |
| **Dtos/**       | **Data Transfer Objects**: simple classes used for requests and responses (e.g. `HouseResponse`, `CreateHouseRequest`) so the API doesn’t expose internal entities directly. |
| **Security/**   | Who the user is (e.g. JWT tokens, org context like `X-Org-Id`). |
| **Migrations/** | Database schema changes over time (tables, columns). |

**Example flow for “get all houses”:**

1. Desktop app sends: `GET https://api.daryva.com/api/houses` (with a login token).
2. **HousesController** receives the request.
3. It calls **HouseService.GetHousesAsync(...)**.
4. **HouseService** uses the **DbContext** to read from the database (and maybe **RentLedgerService** for rent totals).
5. The result is mapped to **HouseResponse** DTOs and sent back as JSON.
6. The desktop app receives the JSON and shows the list in the UI.

So: **Controller** = “front door” of the API; **Service** = “brain”; **Domain/Data** = “what’s in the database”.

---

## Part 6: How the UI and API Connect

- The desktop app does **not** talk to the database directly (in normal, “API mode”).
- It talks to the **API** over **HTTP** (like a web browser):
  - **GET** = “give me data” (e.g. list of houses).
  - **POST** = “create something” (e.g. add a tenant).
  - **PUT** = “update something.”
  - **DELETE** = “remove something.”
- The API checks the **JWT token** (login) and **organization** (e.g. `X-Org-Id`) so each customer only sees their own data.

Under `src/Daryva.UI/Services/` you’ll find things like:

- **Api/** – HTTP client that calls the real API (e.g. `HouseApiService` calling `/api/houses`).
- **Business/** – Adapters that the ViewModels use; they might call the API or, in legacy mode, local SQLite.

So: **ViewModel → Service (adapter) → API client → API → Database.**

---

## Part 7: If You Were to Start From the Beginning (Step-by-Step)

A possible order to learn and build, as a novice:

### Step 1: Understand the idea

- There is an **app** (what the user sees).
- There is a **server** (the API) that does the work and stores data.
- They talk over the **network** with clear “requests” and “responses”.

### Step 2: Learn the basics of one language and one UI

- This project uses **C#** (pronounced “C sharp”). Learn basic C#: variables, if/else, loops, classes, methods.
- For the desktop app, the UI is described in **AXAML** (Avalonia’s markup). Learning a bit of “markup” (tags and attributes) helps.

### Step 3: Run the project

- Install **.NET 8 SDK** and an editor (e.g. **Visual Studio** or **Cursor**).
- Open **Daryva-Avalonia.sln**.
- Build: `dotnet build Daryva-Avalonia.sln`.
- Run API + UI using the script: `.\Scripts\restart-dev.ps1` (see `Docs/workflow-after-changes-and-deploy.md`).

You don’t have to understand every file; just get to “I can open the app and see the Houses screen.”

### Step 4: Follow one feature end-to-end

Pick one simple feature (e.g. “list of houses”):

1. Find the **View**: `Daryva.UI` → `MVVM` → `Views` → something like `HousesView.axaml`.
2. Find the **ViewModel**: `HousesViewModel.cs` – see how it loads `Houses` and what commands it has.
3. Find the **service** the ViewModel uses (e.g. `IHouseService` / `HouseApiServiceAdapter`).
4. Find the **API endpoint**: in `Daryva.Api` → `Controllers` → `HousesController` (e.g. `GET /api/houses`).
5. Find the **service** in the API: `HouseService.cs` – see how it reads from the database and returns data.

Following one path (click “Houses” → what code runs?) teaches you the flow.

### Step 5: Make a tiny change

- Change a label on the Houses screen (in the View).
- Add a property to the ViewModel and show it in the View (binding).
- Change a message or a validation in the API (Controller or Service).

Build and run again to see your change.

### Step 6: Learn a bit about databases and HTTP

- **Database**: “Tables” (e.g. House, Tenant) and “columns” (Name, Address). The API uses **Entity Framework** to turn C# classes into tables and queries.
- **HTTP**: GET/POST/PUT/DELETE and “status codes” (200 OK, 404 Not Found). The API returns **JSON** (structured text) so the app can parse it.

You don’t need to be an expert; enough to read a simple endpoint and a simple table.

### Step 7: Use the docs and workflow

- **`Docs/workflow-after-changes-and-deploy.md`** – What to do after changing code (build, test, deploy).
- When you add or change something, build, run locally, then commit and push. For API changes, pushing to `master` can trigger deploy (see that doc).

---

## Part 8: Glossary (Simple Definitions)

| Term | Meaning |
|------|--------|
| **API** | A program that other programs talk to over the network. It “serves” data and actions (e.g. “give me houses”, “save this tenant”). |
| **Endpoint** | One specific URL and method (e.g. `GET /api/houses`) that does one thing. |
| **ViewModel** | The part of the UI layer that holds data and logic for one screen; the View binds to it. |
| **View** | The actual visual screen (windows, buttons, lists). |
| **Model** | A simple class that represents a piece of data (e.g. House, Tenant). |
| **Service** | A class that does a job (e.g. “get houses from the API”, “calculate rent total”). |
| **Controller** | In the API, the class that receives HTTP requests and calls the right service, then returns the response. |
| **DbContext** | In the API, the object that represents the database and is used to read/write entities. |
| **DTO** | Data Transfer Object – a simple class used to send or receive data over the API (e.g. `HouseResponse`). |
| **JWT** | A secure “token” that proves the user is logged in; the app sends it with each API request. |
| **MVVM** | Pattern: Model, View, ViewModel – separates data, screen, and logic. |
| **Binding** | Connecting a value in the ViewModel to something on the screen so it updates automatically. |

---

## Part 9: Where to Look for What

| You want to… | Look here |
|--------------|-----------|
| Change how a **screen looks** | `src/Daryva.UI/MVVM/Views/*.axaml` |
| Change what a **screen does** (load data, buttons) | `src/Daryva.UI/MVVM/ViewModels/*.cs` |
| Change how the app **talks to the API** | `src/Daryva.UI/Services/Api/` and `Services/Business/*Adapter*.cs` |
| Add or change an **API endpoint** | `src/Daryva.Api/Controllers/*.cs` |
| Change the **business logic** of the API | `src/Daryva.Api/Services/*.cs` |
| Change **database structure** (tables/columns) | `src/Daryva.Api/Domain/*.cs` and `Migrations/`, and possibly `src/Daryva.Data/` |
| Change **auth** (login, tokens, org) | `src/Daryva.Api/Security/`, `Controllers/Auth*` |
| Run the app locally | `.\Scripts\restart-dev.ps1` or see `Docs/workflow-after-changes-and-deploy.md` |
| Deploy the API | Push to `master` (see workflow doc) or run “Deploy API” in GitHub Actions |

---

## Part 10: Summary

- **Daryva** = property/tenant management: houses, tenants, rent, payments, etc.
- **Three layers**: Desktop app (UI) → API (backend) → Database.
- **Daryva.UI** = Avalonia desktop app, **MVVM** (Models, Views, ViewModels), **Services** to call the API.
- **Daryva.Api** = ASP.NET Core API: **Controllers** (endpoints), **Services** (logic), **Domain/Data** (database), **Dtos** (request/response shapes).
- To understand the project: run it, then follow **one feature** from the UI to the API to the database.
- To start from scratch as a novice: learn basic C#, run the solution, follow one flow, make a small change, then use the workflow doc for build and deploy.

Use **`Docs/workflow-after-changes-and-deploy.md`** for the day-to-day commands (build, test, migrate, deploy) after you change code.
