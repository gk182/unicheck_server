from io import BytesIO
import math

import cv2
import face_recognition
import numpy as np
from PIL import Image, ImageOps

MAX_IMAGE_SIZE = (1400, 1400)
DEFAULT_THRESHOLD = 0.52
DEFAULT_MARGIN = 0.04
DEFAULT_NUM_JITTERS = 5
DEFAULT_VERIFY_MIN_MATCHES = 2
VECTOR_DUPLICATE_DISTANCE = 0.08
FACE_LOCATIONS_MODELS = ("hog", "cnn")


def _read_upload_bytes(upload_file):
    file_obj = upload_file.file
    if hasattr(file_obj, "seek"):
        file_obj.seek(0)
    image_bytes = file_obj.read()
    if not image_bytes:
        return None, "File anh rong hoac khong doc duoc"
    return image_bytes, None


def _normalize_image_bytes(image_bytes):
    pil_image = Image.open(BytesIO(image_bytes))
    pil_image = ImageOps.exif_transpose(pil_image).convert("RGB")
    pil_image.thumbnail(MAX_IMAGE_SIZE, Image.Resampling.LANCZOS)

    image = np.array(pil_image, dtype=np.uint8, copy=True)
    if image.ndim != 3 or image.shape[2] != 3:
        return None, "Anh dau vao khong hop le"

    return image, None


def _normalize_uploaded_image(upload_file):
    image_bytes, error = _read_upload_bytes(upload_file)
    if image_bytes is None:
        return None, error
    return _normalize_image_bytes(image_bytes)


def _build_preprocessed_variants(image):
    variants = [image]

    ycrcb = cv2.cvtColor(image, cv2.COLOR_RGB2YCrCb)
    y_channel, cr_channel, cb_channel = cv2.split(ycrcb)

    clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8))
    y_clahe = clahe.apply(y_channel)
    clahe_image = cv2.cvtColor(
        cv2.merge((y_clahe, cr_channel, cb_channel)),
        cv2.COLOR_YCrCb2RGB,
    )
    variants.append(clahe_image)

    brighter = cv2.convertScaleAbs(clahe_image, alpha=1.08, beta=8)
    softer = cv2.convertScaleAbs(image, alpha=0.95, beta=4)
    variants.extend((brighter, softer))

    unique_variants = []
    seen = set()
    for variant in variants:
        key = variant.tobytes()
        if key in seen:
            continue
        seen.add(key)
        unique_variants.append(np.ascontiguousarray(variant))

    return unique_variants


def _detect_single_face(image):
    last_locations = []
    for model in FACE_LOCATIONS_MODELS:
        try:
            locations = face_recognition.face_locations(image, model=model)
        except RuntimeError:
            continue

        if len(locations) == 1:
            return locations[0], None
        if locations:
            last_locations = locations

    if len(last_locations) > 1:
        return None, "Anh co nhieu nguoi. Vui long chi de 1 khuon mat trong khung hinh!"

    return None, "Khong tim thay khuon mat nao. Hay dua mat vao khung hinh ro hon!"


def _extract_robust_embedding(image, num_jitters=DEFAULT_NUM_JITTERS):
    variants = _build_preprocessed_variants(image)
    collected_encodings = []
    error_message = None

    for variant in variants:
        location, error_message = _detect_single_face(variant)
        if location is None:
            continue

        encodings = face_recognition.face_encodings(
            variant,
            known_face_locations=[location],
            num_jitters=num_jitters,
        )
        if encodings:
            collected_encodings.append(encodings[0])

    if not collected_encodings:
        return None, error_message or "Khong tao duoc vector khuon mat"

    merged_encoding = np.mean(np.vstack(collected_encodings), axis=0)
    return merged_encoding.astype(np.float64), "Thanh cong"


def _normalize_known_vectors(known_vector_input):
    known_array = np.asarray(known_vector_input, dtype=np.float64)

    if known_array.ndim == 1:
        if known_array.size == 0:
            raise ValueError("Vector rong")
        return np.expand_dims(known_array, axis=0)

    if known_array.ndim == 2:
        if known_array.shape[0] == 0:
            raise ValueError("Danh sach vector rong")
        return known_array

    raise ValueError("Dinh dang vector khong hop le")


def _distance_to_confidence(distance, threshold):
    soft_window = 0.10
    strong_match = max(0.0, threshold - soft_window)

    if distance <= strong_match:
        confidence = 99.0 - (distance / max(strong_match, 1e-6)) * 9.0
    elif distance <= threshold:
        ratio = (distance - strong_match) / max(threshold - strong_match, 1e-6)
        confidence = 90.0 - ratio * 20.0
    else:
        ratio = min((distance - threshold) / max(1.0 - threshold, 1e-6), 1.0)
        confidence = 70.0 - ratio * 70.0

    return round(float(np.clip(confidence, 0.0, 99.0)), 2)


def _compare_vector_to_known(vector, known_vectors, threshold):
    distances = face_recognition.face_distance(known_vectors, vector)
    best_index = int(np.argmin(distances))
    best_distance = float(distances[best_index])

    return {
        "best_distance": best_distance,
        "best_index": best_index,
        "is_match": best_distance <= threshold,
        "confidence": _distance_to_confidence(best_distance, threshold),
    }


def _deduplicate_vectors(vectors, duplicate_distance=VECTOR_DUPLICATE_DISTANCE):
    unique_vectors = []

    for vector in vectors:
        vector_array = np.asarray(vector, dtype=np.float64)
        if not unique_vectors:
            unique_vectors.append(vector_array)
            continue

        distances = face_recognition.face_distance(np.vstack(unique_vectors), vector_array)
        if float(np.min(distances)) > duplicate_distance:
            unique_vectors.append(vector_array)

    return [vector.tolist() for vector in unique_vectors]


def vectorize_image_bytes(image_bytes, num_jitters=DEFAULT_NUM_JITTERS):
    image, error = _normalize_image_bytes(image_bytes)
    if image is None:
        return None, error

    return _extract_robust_embedding(image, num_jitters=num_jitters)


def image_to_vector(upload_file, num_jitters=DEFAULT_NUM_JITTERS):
    try:
        image_bytes, error = _read_upload_bytes(upload_file)
        if image_bytes is None:
            return None, error

        encoding, message = vectorize_image_bytes(image_bytes, num_jitters=num_jitters)
        if encoding is None:
            return None, message

        return encoding.tolist(), message
    except Exception as exc:
        return None, f"Loi xu ly anh: {exc}"


def images_to_vectors(upload_files, num_jitters=DEFAULT_NUM_JITTERS):
    vectors = []
    details = []

    for index, upload_file in enumerate(upload_files):
        try:
            vector, message = image_to_vector(upload_file, num_jitters=num_jitters)
            if vector is None:
                details.append(
                    {
                        "index": index,
                        "success": False,
                        "message": message,
                    }
                )
                continue

            vectors.append(vector)
            details.append(
                {
                    "index": index,
                    "success": True,
                    "message": message,
                }
            )
        except Exception as exc:
            details.append(
                {
                    "index": index,
                    "success": False,
                    "message": f"Loi xu ly anh: {exc}",
                }
            )

    return _deduplicate_vectors(vectors), details


def check_match(
    upload_file,
    known_vector_list,
    threshold=DEFAULT_THRESHOLD,
    margin=DEFAULT_MARGIN,
    num_jitters=DEFAULT_NUM_JITTERS,
):
    try:
        unknown_vector, msg = image_to_vector(upload_file, num_jitters=num_jitters)
        if unknown_vector is None:
            return False, 0.0, msg, None

        known_vectors = _normalize_known_vectors(known_vector_list)
        compare_result = _compare_vector_to_known(
            np.asarray(unknown_vector, dtype=np.float64),
            known_vectors,
            threshold + margin,
        )

        return (
            compare_result["is_match"],
            compare_result["confidence"],
            f"So sanh hoan tat (distance={compare_result['best_distance']:.4f})",
            compare_result,
        )
    except Exception as exc:
        return False, 0.0, f"Loi so sanh: {exc}", None


def check_match_multiple(
    upload_files,
    known_vector_list,
    threshold=DEFAULT_THRESHOLD,
    margin=DEFAULT_MARGIN,
    num_jitters=DEFAULT_NUM_JITTERS,
    min_matches=DEFAULT_VERIFY_MIN_MATCHES,
):
    try:
        known_vectors = _normalize_known_vectors(known_vector_list)
        per_image_results = []
        valid_results = []
        effective_threshold = threshold + margin

        for index, upload_file in enumerate(upload_files):
            vector, message = image_to_vector(upload_file, num_jitters=num_jitters)
            if vector is None:
                per_image_results.append(
                    {
                        "index": index,
                        "success": False,
                        "message": message,
                    }
                )
                continue

            compare_result = _compare_vector_to_known(
                np.asarray(vector, dtype=np.float64),
                known_vectors,
                effective_threshold,
            )
            image_result = {
                "index": index,
                "success": True,
                "message": "Thanh cong",
                "best_distance": round(compare_result["best_distance"], 4),
                "confidence": compare_result["confidence"],
                "is_match": compare_result["is_match"],
                "best_vector_index": compare_result["best_index"],
            }
            per_image_results.append(image_result)
            valid_results.append(compare_result)

        if not valid_results:
            return {
                "is_match": False,
                "confidence": 0.0,
                "message": "Khong co anh nao tao duoc vector hop le",
                "best_distance": None,
                "matched_frames": 0,
                "valid_frames": 0,
                "required_matches": min_matches,
                "per_image_results": per_image_results,
            }

        valid_count = len(valid_results)
        required_matches = min(max(1, min_matches), valid_count)
        matched_results = [result for result in valid_results if result["is_match"]]
        matched_count = len(matched_results)

        sorted_distances = sorted(result["best_distance"] for result in valid_results)
        top_k = min(required_matches, len(sorted_distances))
        aggregate_distance = float(np.mean(sorted_distances[:top_k]))
        best_distance = float(sorted_distances[0])

        is_match = matched_count >= required_matches and aggregate_distance <= effective_threshold
        aggregate_confidence = _distance_to_confidence(aggregate_distance, effective_threshold)

        if is_match:
            message = (
                f"Xac minh thanh cong voi {matched_count}/{valid_count} frame hop le "
                f"(aggregate_distance={aggregate_distance:.4f})"
            )
        else:
            message = (
                f"Khong du dieu kien xac minh. Match {matched_count}/{valid_count} frame "
                f"(aggregate_distance={aggregate_distance:.4f})"
            )

        return {
            "is_match": is_match,
            "confidence": aggregate_confidence,
            "message": message,
            "best_distance": round(best_distance, 4),
            "aggregate_distance": round(aggregate_distance, 4),
            "matched_frames": matched_count,
            "valid_frames": valid_count,
            "required_matches": required_matches,
            "per_image_results": per_image_results,
        }
    except Exception as exc:
        return {
            "is_match": False,
            "confidence": 0.0,
            "message": f"Loi so sanh nhieu anh: {exc}",
            "best_distance": None,
            "matched_frames": 0,
            "valid_frames": 0,
            "required_matches": 0,
            "per_image_results": [],
        }


def build_verify_settings(frame_count):
    if frame_count <= 1:
        return 1
    return min(frame_count, max(DEFAULT_VERIFY_MIN_MATCHES, math.ceil(frame_count / 2)))
