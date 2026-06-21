with open('e:/AutoScript/FlowAuto/TestScripts/测试方法与文档.md', 'r', encoding='utf-8') as f:
    lines = f.readlines()

for i, line in enumerate(lines):
    if '方向检测' in line:
        print(f'Line {i+1}: {line.rstrip()}')
