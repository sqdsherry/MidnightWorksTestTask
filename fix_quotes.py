import os

files = os.popen('git diff --name-only main...feature/12a-visual-world').read().splitlines()
count = 0
for path in files:
    if not os.path.exists(path) or not path.endswith('.cs'):
        continue
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    if '""' in content:
        new_content = content.replace('""', '"')
        with open(path, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print(f"Fixed {path}")
        count += 1

print(f"Fixed {count} files.")
