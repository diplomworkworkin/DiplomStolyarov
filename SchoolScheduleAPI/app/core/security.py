from __future__ import annotations

import base64
import hashlib
import hmac
import secrets


_ALGORITHM = "pbkdf2_sha256"
_ITERATIONS = 260_000
_SALT_BYTES = 16


def is_password_hashed(password: str | None) -> bool:
    if not password:
        return False
    parts = password.split("$")
    return len(parts) == 4 and parts[0] == _ALGORITHM


def hash_password(password: str) -> str:
    if is_password_hashed(password):
        return password

    salt = secrets.token_bytes(_SALT_BYTES)
    digest = hashlib.pbkdf2_hmac(
        "sha256",
        password.encode("utf-8"),
        salt,
        _ITERATIONS,
    )
    salt_b64 = base64.b64encode(salt).decode("ascii")
    digest_b64 = base64.b64encode(digest).decode("ascii")
    return f"{_ALGORITHM}${_ITERATIONS}${salt_b64}${digest_b64}"


def verify_password(plain_password: str, stored_password: str | None) -> bool:
    if not stored_password:
        return False

    if not is_password_hashed(stored_password):
        return plain_password == stored_password

    try:
        _, iterations_text, salt_b64, digest_b64 = stored_password.split("$")
        iterations = int(iterations_text)
        salt = base64.b64decode(salt_b64.encode("ascii"))
        expected_digest = base64.b64decode(digest_b64.encode("ascii"))
    except (ValueError, TypeError):
        return False

    actual_digest = hashlib.pbkdf2_hmac(
        "sha256",
        plain_password.encode("utf-8"),
        salt,
        iterations,
    )
    return hmac.compare_digest(actual_digest, expected_digest)
