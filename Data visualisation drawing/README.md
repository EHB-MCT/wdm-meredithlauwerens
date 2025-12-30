# Drawing data visualisation project
This project is a Unity-based drawing experiment tool that tracks user drawing behavior, including strokes, color changes, and editing actions. A Node.js backend stores the data in PostgreSQL, and an admin dashboard visualizes user statistics with interactive charts. Docker can be used to run the backend and database in a containerized environment.
(WMD: weapon of math destruction)

## Features
Unity client
- Users are assigned a unique ID (UID) each session
- Supports drawing with multiple strokes and colors
- Tracks:
    - Stroke duration
    - Stroke color
    - Color changes
    - Undo/redo events
    - Erases
    - Stroke width changes
- Sends drawing data to a PostgreSQL backend via REST API

Backend(Node.js + Express)
- Stores drawing data in PostgreSQL
- Provides endpoints to:
    - Receive strokes, drawings, and sessions from Unity
    - Fetch aggregated statistics per user
    - Fetch per-topic stroke, color, and bounds data for dashboard charts

Admin dashboard (HTML + Chart.js)
- View user-specific drawing statistics:
    - Average duration per drawing
    - Undo/erase behavior
    - Reference usage per topic
    - Color changes and which colors were used
    - Total strokes per topic
    - Drawing area (“bounds”) per topic
- Charts are interactive and responsive
- Dashboard designed to be clean and visually appealing

## Technology stack
- Unity: C# for drawing input and sending data
- Node.js / Express: Backend API
- PostgreSQL: Database to store strokes, drawings, sessions, and topics
- Chart.js: Dashboard visualizations
- HTML/CSS/JS: Web dashboard UI
- Docker: Containerized backend and database setup

## Setup instructions
### Backend
1. Clone the repository.
2. Create a PostgreSQL database.
3. Set DATABASE_URL environment variable: 
export DATABASE_URL=postgres://user:password@localhost:5432/dbname
4. Run database initialization SQL: 
-- init.sql
CREATE TABLE strokes (...);
CREATE TABLE drawings (...);
CREATE TABLE topic_drawings (...);
CREATE TABLE events (...);
CREATE TABLE sessions (...);
5. Install dependencies and start server:
npm install
node server.js
Server runs on http://localhost:5000

### Docker
1. Ensure Docker and Docker Compose are installed
2. Use provided docker-compose.yml (or create one) to run PostgreSQL + Node backend:
version: '3'
services:
  db:
    image: postgres:15
    environment:
      POSTGRES_USER: user
      POSTGRES_PASSWORD: password
      POSTGRES_DB: drawings_db
    ports:
      - "5432:5432"
  backend:
    build: .
    environment:
      DATABASE_URL: postgres://user:password@db:5432/drawings_db
    ports:
      - "5000:5000"
    depends_on:
      - db
3. Run
docker-compose up
4. Backend is available at http://localhost:5000

### Unity client
1. Open Unity project
2. Attach the DrawManager script to the relevant GameObject
3. Ensure Start() initializes a new UID per session
4. Configure API URLs to point to your backend (http://localhost:5000/api/...)
5. Run the scene and draw

### Admin dashboard
1. Open index.html in a browser
2. The dashboard fetches users and drawing data automatically
3. Use charts to analyze drawing behavior
4. Ensure style.css is linked for proper layout and responsive charts

## Usage
- Dashboard: Select a user from the dropdown to see their drawing statistics.
- Charts:
    - Line chart: Average drawing duration
    - Bar chart: Undo & erase counts
    - Pie chart: Reference usage
    - Pie chart: Color changes
    - Bar chart: Strokes per topic
    - Bar chart: Drawing area (bounds) per topic

## License
MIT license

## Sources
### AI conversations
- Inspiration and beginning project (ChatGPT): https://chatgpt.com/share/6953e23f-3430-8011-8ee9-dc6de90a8ff7
- Adding reference images to game (ChatGPT): https://chatgpt.com/share/6953e281-5e0c-8011-80ef-a106946d6930
- Fixing rounding duration error (ChatGPT): https://chatgpt.com/share/6953e299-33e0-8011-9c7a-a52c4ebb9642 
- Creating admin dashboard with charts for data visualisation (ChatGPT): https://chatgpt.com/share/6953e2cf-c9ac-8011-abca-9c60a53c7924 