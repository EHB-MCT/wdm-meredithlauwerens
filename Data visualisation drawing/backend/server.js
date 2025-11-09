//a simple express API that captures and stores Unity data in PostgreSQL

import express from "express";
import bodyParser from "body-parser";
import cors from "cors";
import pkg from "pg";

const { Pool } = pkg;

const app = express();
app.use(cors());
app.use(bodyParser.json());

//database connection via environment variable
const pool = new Pool({
	connectionString: process.env.DATABASE_URL,
});

//test endpoint
app.get("/", (req, res) => {
	res.send("✅ Drawing API is running!");
});

//receive Unity-data
app.post("/api/strokes", async (req, res) => {
	try {
		const { uid, color, duration, points } = req.body;
		if (!uid || !color || !points) {
			return res.status(400).send("Missing required fields");
		}

		await pool.query("INSERT INTO strokes (uid, color, duration, points) VALUES ($1, $2, $3, $4)", [uid, color, duration, JSON.stringify(points)]);

		res.status(200).send("Data saved");
	} catch (err) {
		console.error(err);
		res.status(500).send("Database error");
	}
});

const PORT = 5000;
app.listen(PORT, "0.0.0.0", () => {
  console.log(`🚀 Server running on http://0.0.0.0:${PORT}`);
});

