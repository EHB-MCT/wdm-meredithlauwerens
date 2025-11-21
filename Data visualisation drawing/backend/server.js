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

		const roundedDuration = Number(duration.toFixed(2)); //Roundup to 2 decimals

		await pool.query("INSERT INTO strokes (uid, color, duration, points) VALUES ($1, $2, $3, $4)", [uid, color, roundedDuration, JSON.stringify(points)]);
		console.log(`Saved stroke: uid=${uid}, color=${color}, duration=${roundedDuration}, points=${points.length}`);
		console.log("Data saved"); // Log success
		res.status(200).send("Data saved");
	} catch (err) {
		console.error("Error saving data:", err); //log errors
		res.status(500).send("Database error");
	}
});

app.post("/api/done", async (req, res) => {
	const { uid, done } = req.body;

	if (!uid || !done) {
		return res.status(400).send("Missing fields");
	}

	console.log(`User ${uid} finished drawing`);

	res.status(200).send("Done received");
});

app.post("/api/drawing", async (req, res) => {
	try {
		const { uid, totalDuration, strokes } = req.body;

		if (!uid || !strokes) {
			return res.status(400).send("Missing required fields");
		}

		console.log(` Full drawing received from user: ${uid}`);
		console.log(`   Strokes: ${strokes.length}`);
		console.log(`   Total duration: ${totalDuration}s`);

		// Opslaan
		await pool.query("INSERT INTO drawings (uid, total_duration, strokes) VALUES ($1, $2, $3)", [uid, totalDuration, JSON.stringify(strokes)]);

		res.status(200).send("Full drawing saved");
	} catch (err) {
		console.error(" Error:", err);
		res.status(500).send("Database error");
	}
});

const PORT = 5000;
app.listen(PORT, "0.0.0.0", () => {
	console.log(` Server running on http://0.0.0.0:${PORT}`);
});
