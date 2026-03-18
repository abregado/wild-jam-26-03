import os

total_lines = 0
file_count = 0

for root, dirs, files in os.walk("."):
    # Skip hidden dirs and common non-source dirs
    dirs[:] = [d for d in dirs if not d.startswith(".") and d not in (".godot",)]
    for f in files:
        if f.endswith(".cs"):
            path = os.path.join(root, f)
            with open(path, encoding="utf-8", errors="ignore") as fh:
                lines = fh.readlines()
            count = len(lines)
            print(f"{count:>6}  {path}")
            total_lines += count
            file_count += 1

print(f"\n{total_lines:>6}  total lines across {file_count} files")
