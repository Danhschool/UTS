STT / ASR models — tải thủ công (không có trên GitHub do >100 MB)
====================================================================

Sau khi clone repo, đặt file vào đúng thư mục StreamingAssets bên dưới.
Unity: Model Path trên WhisperManager = đường dẫn tương đối trong StreamingAssets (vd. Whisper/ggml-base.bin).

--- Whisper (com.whisper.unity) — thư mục: Assets/StreamingAssets/Whisper/ ---

Nguồn: https://huggingface.co/ggerganov/whisper.cpp/tree/main
Cấu hình tiếng Việt: Language = vi

| File                  | ~Size  | Ghi chú                          |
|-----------------------|--------|----------------------------------|
| ggml-base.bin         | 148 MB | Mặc định, đa ngôn ngữ (vi)       |
| ggml-small-q8_0.bin   | 264 MB | Khuyên dùng — cân bằng cho RTS   |
| ggml-small.bin        | 488 MB | Chất lượng cao, nặng máy         |

Link tải trực tiếp (resolve/main):

  ggml-base.bin
    https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin

  ggml-small-q8_0.bin
    https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small-q8_0.bin

  ggml-small.bin
    https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin

Sau khi tải, sửa Model Path trên WhisperManager (vd.):
  Whisper/ggml-base.bin
  Whisper/ggml-small-q8_0.bin

--- Vosk (tiếng Việt) — thư mục: Assets/StreamingAssets/VoskModels/ ---

Nguồn: https://alphacephei.com/vosk/models

| Model / zip                    | ~Size  | Ghi chú                    |
|--------------------------------|--------|----------------------------|
| vosk-model-small-vn-0.4.zip    |  34 MB | Nhẹ, sandbox / thử nhanh   |
| vosk-model-vn-0.4.zip          |  71 MB | Độ chính xác cao hơn       |

Link tải:

  vosk-model-small-vn-0.4.zip
    https://alphacephei.com/vosk/models/vosk-model-small-vn-0.4.zip

  vosk-model-vn-0.4.zip
    https://alphacephei.com/vosk/models/vosk-model-vn-0.4.zip

Giải nén zip vào VoskModels/ sao cho có thư mục:
  VoskModels/vosk-model-small-vn-0.4/
  VoskModels/vosk-model-vn-0.4/

(File graph/HCLG.fst của model lớn >100 MB — không push Git; cần giải nén đủ từ zip ở trên.)
