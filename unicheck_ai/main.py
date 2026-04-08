import json

import uvicorn
from fastapi import FastAPI, File, Form, UploadFile

import face_logic

app = FastAPI()


@app.get("/")
def root():
    return {"message": "UniCheck AI Service is Running..."}


@app.post("/register-face")
async def register_face(file: UploadFile = File(...)):
    vector, msg = face_logic.image_to_vector(file)

    if vector is None:
        return {
            "success": False,
            "message": msg,
            "vector": [],
        }

    return {
        "success": True,
        "message": "Create vector success",
        "vector": vector,
    }


@app.post("/register-face-batch")
async def register_face_batch(files: list[UploadFile] = File(...)):
    vectors, details = face_logic.images_to_vectors(files)

    if not vectors:
        return {
            "success": False,
            "message": "Khong tao duoc vector hop le tu cac anh da gui",
            "vectors": [],
            "details": details,
        }

    return {
        "success": True,
        "message": f"Tao thanh cong {len(vectors)} vector",
        "vectors": vectors,
        "details": details,
    }


@app.post("/verify-face")
async def verify_face(
    file: UploadFile = File(...),
    known_vector: str = Form(...),
):
    try:
        known_vector_list = json.loads(known_vector)
    except Exception:
        return {
            "success": False,
            "is_match": False,
            "confidence": 0.0,
            "message": "Vector not valid",
        }

    is_match, confidence, msg, detail = face_logic.check_match(file, known_vector_list)

    return {
        "success": True,
        "is_match": bool(is_match),
        "confidence": confidence,
        "message": msg,
        "detail": detail,
    }


@app.post("/verify-face-batch")
async def verify_face_batch(
    files: list[UploadFile] = File(...),
    known_vectors: str = Form(...),
    min_matches: int | None = Form(default=None),
):
    try:
        known_vector_list = json.loads(known_vectors)
    except Exception:
        return {
            "success": False,
            "is_match": False,
            "confidence": 0.0,
            "message": "Known vectors not valid",
            "result": None,
        }

    required_matches = (
        min_matches
        if min_matches is not None
        else face_logic.build_verify_settings(len(files))
    )

    result = face_logic.check_match_multiple(
        files,
        known_vector_list,
        min_matches=required_matches,
    )

    return {
        "success": True,
        "is_match": bool(result["is_match"]),
        "confidence": result["confidence"],
        "message": result["message"],
        "result": result,
    }


if __name__ == "__main__":
    uvicorn.run(
        "main:app",
        host="localhost",
        port=8000,
        reload=True,
    )
