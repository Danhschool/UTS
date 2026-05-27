Whisper STT (com.whisper.unity) — model trong StreamingAssets.

Đã có sẵn: ggml-base.bin (multilingual, tiếng Việt).

Khuyên nâng cấp (tải từ https://huggingface.co/ggerganov/whisper.cpp/tree/main):
  ggml-small-q8_0.bin  (~264 MB) — cân bằng tốt cho lệnh RTS

Đặt file vào thư mục này, rồi sửa Model Path trên WhisperManager:
  Whisper/ggml-small-q8_0.bin

Cấu hình tiếng Việt: Language = vi
