#! /bin/sh
cd ~/llama.cpp

./build/bin/llama-server \
  --model /models/Qwen3.5-35B-A3B-Uncensored-HauhauCS-Aggressive-Q5_K_M.gguf \
  --alias "Qwen3.5-35B-A3B-Q5_K_M" \
  --host 127.0.0.1 \
  --port 8081 \
  --ctx-size 16384 \
  --jinja \
  --reasoning-budget 0 \
  --n-gpu-layers 99 \
  --n-cpu-moe 25 \
  --flash-attn on \
  --temp 0.1 \
  --top-p 0.9 \
  --min-p 0.03 \
  --repeat-penalty 1.03 \
  --cache-type-k q8_0 \
  --cache-type-v q8_0 \
  --no-mmap \
  --threads 10
