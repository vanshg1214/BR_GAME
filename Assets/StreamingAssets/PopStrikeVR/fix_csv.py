import os
import glob
import math

MIN_DIST_TMT = 15.0  # Minimum 15 degrees apart
MIN_DIST_TRACE = 2.0 # Remove duplicates in traces closer than 2 degrees

def parse_coords(coord_str):
    tokens = coord_str.split(';')
    coords = []
    for t in tokens:
        t = t.strip()
        if not t: continue
        t = t.replace('(', '').replace(')', '')
        parts = t.split(',')
        if len(parts) >= 2:
            coords.append([float(parts[0]), float(parts[1])])
    return coords

def serialize_coords(coords):
    return ';'.join([f"({int(round(c[0]))},{int(round(c[1]))})" for c in coords])

def relax_coords(coords, min_dist):
    # Iterative relaxation
    max_iters = 100
    for _ in range(max_iters):
        moved = False
        for i in range(len(coords)):
            for j in range(i + 1, len(coords)):
                dx = coords[i][0] - coords[j][0]
                dy = coords[i][1] - coords[j][1]
                dist = math.hypot(dx, dy)
                if dist < min_dist:
                    moved = True
                    # Push apart along the vector
                    if dist == 0:
                        dx, dy = 1.0, 0.0
                        dist = 1.0
                    push_amt = (min_dist - dist) / 2.0
                    push_x = (dx / dist) * push_amt
                    push_y = (dy / dist) * push_amt
                    
                    coords[i][0] += push_x
                    coords[i][1] += push_y
                    coords[j][0] -= push_x
                    coords[j][1] -= push_y
        
        # Clamp to bounds -60 to 60 (canonical CSV range)
        for c in coords:
            c[0] = max(-60.0, min(60.0, c[0]))
            c[1] = max(-60.0, min(60.0, c[1]))

        if not moved:
            break
    return coords

def clean_trace_coords(coords, min_dist):
    if not coords: return []
    cleaned = [coords[0]]
    for i in range(1, len(coords)):
        dx = coords[i][0] - cleaned[-1][0]
        dy = coords[i][1] - cleaned[-1][1]
        dist = math.hypot(dx, dy)
        if dist >= min_dist:
            cleaned.append(coords[i])
    return cleaned

def process_file(filepath):
    print(f"Processing {filepath}...")
    with open(filepath, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    new_lines = []
    for line in lines:
        clean_line = line.strip()
        if not clean_line:
            new_lines.append(line)
            continue
        
        parts = clean_line.split(',', 1)
        if len(parts) != 2:
            new_lines.append(line)
            continue
            
        task_type = parts[0].strip()
        coord_str = parts[1].strip()
        
        coords = parse_coords(coord_str)
        
        if task_type in ['T', 'O']:
            coords = relax_coords(coords, MIN_DIST_TMT)
        elif task_type in ['B', 'G']:
            coords = clean_trace_coords(coords, MIN_DIST_TRACE)
            
        new_coord_str = serialize_coords(coords)
        new_lines.append(f"{task_type}, {new_coord_str}\n")
        
    with open(filepath, 'w', encoding='utf-8') as f:
        f.writelines(new_lines)

def main():
    search_path = os.path.join(os.getcwd(), '**', '*.csv')
    files = glob.glob(search_path, recursive=True)
    count = 0
    for f in files:
        if 'PopStrikeVR' in f: # Ensure we only touch these files
            process_file(f)
            count += 1
    print(f"Processed {count} files.")

if __name__ == '__main__':
    main()
