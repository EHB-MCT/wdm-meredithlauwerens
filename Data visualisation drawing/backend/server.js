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
	res.send("Drawing API is running!");
});

//receive Unity-data
app.post("/api/strokes", async (req, res) => {
	try {
		const { uid, color, duration, points } = req.body;

		if (!uid || !color || !points) {
			return res.status(400).send("Missing required fields");
		}

		const roundedDuration = Number(duration.toFixed(2));

		await pool.query("INSERT INTO strokes (uid, color, duration, points) VALUES ($1, $2, $3, $4)", [uid, color, roundedDuration, JSON.stringify(points)]);

		console.log(`Saved stroke: uid=${uid}, color=${color}, duration=${roundedDuration}, points=${points.length}`);

		res.status(200).send("Data saved");
	} catch (err) {
		console.error("Error saving data:", err);
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
		const { uid, totalDuration, strokes, colorChangeCount, eraseCount, undoCount, redoCount, increaseWidthCount, decreaseWidthCount } = req.body;

		if (!uid || !strokes) {
			return res.status(400).send("Missing required fields");
		}

		//check so that toFixed is never called on undefined
		const safeDuration = Number(totalDuration) || 0;
		const roundedDuration = Math.round(safeDuration * 100) / 100;

		const drawingData = {
			uid,
			totalDuration: roundedDuration,
			strokeCount: strokes.length,
			colorChangeCount: colorChangeCount || 0,
			eraseCount: eraseCount || 0, //total eraser used
			undoCount: undoCount || 0, //total undo used
			redoCount: redoCount || 0, //total redo used
            increaseWidthCount: increaseWidthCount || 0, //total increase stroke width used
            decreaseWidthCount: decreaseWidthCount || 0, //total decrease stroke width used
			strokes: strokes.map((s) => ({
				uid,
				color: s.color,
				duration: Number(s.duration.toFixed(2)),
			})),
		};


		console.log("Full drawing JSON:\n", JSON.stringify(drawingData, null, 2));

		await pool.query("INSERT INTO drawings (uid, total_duration, strokes, color_change_count, erase_count, undo_count, redo_count, increase_width_count, decrease_width_count) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)", [uid, roundedDuration, JSON.stringify(strokes), colorChangeCount || 0, eraseCount || 0, undoCount || 0, redoCount || 0, increaseWidthCount || 0, decreaseWidthCount || 0]);

		res.status(200).send("Full drawing saved");
	} catch (err) {
		console.error("Error:", err);
		res.status(500).send("Database error");
	}
});

app.post("/api/session", async (req, res) => {
    try {
        const { uid, session } = req.body;
        if (!uid || !session) {
            console.warn("Missing uid or session in payload");
            return res.status(400).send("Missing fields");
        }

        console.log("Received session data:", JSON.stringify(req.body, null, 2));

		for (const topicData of session) {
			const strokes = topicData.drawing?.strokes || [];
			await pool.query(
				"INSERT INTO topic_drawings (uid, topic, used_reference, strokes) VALUES ($1, $2, $3, $4)",
				[uid, topicData.topic, topicData.usedReference, JSON.stringify(strokes)]
			);
			if (!topicData.drawing || strokes.length === 0) {
				console.warn(`Topic "${topicData.topic}" has no drawing data for user ${uid}.`);
			}
		}


        res.status(200).send("Session saved");
    } catch (err) {
        console.error("Error saving session:", err);
        res.status(500).send("Database error");
    }
});


const PORT = 5000;
app.listen(PORT, "0.0.0.0", () => {
	console.log(` Server running on http://0.0.0.0:${PORT}`);
});
