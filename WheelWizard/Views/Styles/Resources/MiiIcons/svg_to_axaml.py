import xml.etree.ElementTree as ET
import os
import argparse
import re
from collections import OrderedDict # To keep color order somewhat consistent

# --- Configuration ---
DEFAULT_UNMAPPED_BRUSH = "Black" # Fallback if keeping named colors fails or unexpected errors
# UPDATED: Increased max template colors
MAX_TEMPLATE_COLORS = 12 # How many TemplateColorX options to offer

# Preview Configuration
PREVIEW_NAMESPACE_UTIL = "clr-namespace:WheelWizard.Styles.Util" # ADJUST THIS
MAX_PREVIEW_ITEMS = 79

# --- Helper Functions ---
def normalize_color(color_str):
    """Converts color to lowercase hex or name."""
    if not color_str:
        return None
    color_str = color_str.lower().strip()
    if color_str == 'none':
        return 'none'
    # Normalize short hex codes (#rgb -> #rrggbb)
    if color_str.startswith('#') and len(color_str) == 4:
       color_str = f"#{color_str[1]*2}{color_str[2]*2}{color_str[3]*2}"
    return color_str

def find_unique_colors_in_svgs(svg_folder):
    """Scans all SVGs in a folder and returns a set of unique normalized colors."""
    unique_colors = set()
    namespaces = {'svg': 'http://www.w3.org/2000/svg'}
    ET.register_namespace('', namespaces['svg'])

    print("Scanning SVGs for unique colors...")
    svg_files = [f for f in os.listdir(svg_folder) if f.lower().endswith('.svg')]
    if not svg_files:
        print("No SVG files found to scan.")
        return set()

    for filename in svg_files:
        svg_path = os.path.join(svg_folder, filename)
        try:
            tree = ET.parse(svg_path)
            root = tree.getroot()
            # Find colors in paths, circles, rects, etc. (adjust if needed)
            elements_with_color = root.findall('.//*[@fill]', namespaces) + \
                                  root.findall('.//*[@stroke]', namespaces)
            # Also check root element style attributes
            elements_with_style = root.findall('.//*[@style]', namespaces)

            for elem in elements_with_color:
                 fill = normalize_color(elem.get('fill'))
                 stroke = normalize_color(elem.get('stroke'))
                 if fill and fill != 'none':
                     unique_colors.add(fill)
                 if stroke and stroke != 'none':
                     unique_colors.add(stroke)

            # Rudimentary style attribute parsing
            for elem in elements_with_style:
                 style_str = elem.get('style', '')
                 # Look for fill: #xxxxxx or fill: name
                 fill_match = re.search(r'fill:\s*([^;]+)', style_str)
                 if fill_match:
                     fill = normalize_color(fill_match.group(1))
                     if fill and fill != 'none':
                        unique_colors.add(fill)
                 # Look for stroke: #xxxxxx or stroke: name
                 stroke_match = re.search(r'stroke:\s*([^;]+)', style_str)
                 if stroke_match:
                     stroke = normalize_color(stroke_match.group(1))
                     if stroke and stroke != 'none':
                         unique_colors.add(stroke)

        except ET.ParseError as e:
            print(f"Warning: Could not parse {filename} during color scan: {e}")
        except FileNotFoundError:
            print(f"Warning: File {filename} not found during color scan.")
        except Exception as e:
             print(f"Warning: An unexpected error occurred scanning {filename}: {e}")


    print(f"Found {len(unique_colors)} unique colors.")
    # Sort for consistent order - hex codes first, then names
    sorted_colors = sorted(list(unique_colors), key=lambda c: (not c.startswith('#'), c))
    return sorted_colors # Return a list for ordered processing

# UPDATED: build_interactive_color_map
def build_interactive_color_map(colors_to_map):
    """Interactively asks the user to map detected colors."""
    dynamic_color_map = {}
    print("\n--- Interactive Color Mapping ---")
    # UPDATED: Instructions reflect new range and no skip option
    print(f"Enter a number (1-{MAX_TEMPLATE_COLORS}) to map to the corresponding TemplateColor.")
    print("Enter '0' to keep the original color (use hex/name directly).")
    print("---------------------------------")

    if not colors_to_map:
        print("No colors found to map.")
        return {}

    for color in colors_to_map:
        while True:
            prompt = f"Map color '{color}': "
            user_input = input(prompt).strip().lower()

            # REMOVED: Skip ('s') option
            # if user_input == 's':
            #     dynamic_color_map[color] = DEFAULT_UNMAPPED_BRUSH # Map to default Brush *value*
            #     print(f"  Mapping '{color}' -> {DEFAULT_UNMAPPED_BRUSH}")
            #     break

            if user_input == '0':
                if color.startswith('#'):
                    dynamic_color_map[color] = color.upper() # Keep original hex
                    print(f"  Keeping '{color}' -> {color.upper()}")
                else:
                    # Keeping named colors directly can be less reliable than hex.
                    # Map named colors kept via '0' to the default brush for safety.
                    # If you trust your named colors (like 'white', 'black'), you could try:
                    # dynamic_color_map[color] = color.capitalize()
                    print(f"  Warning: Keeping named color '{color}' directly might not work reliably.")
                    print(f"  Mapping '{color}' (requested keep) -> {DEFAULT_UNMAPPED_BRUSH}")
                    dynamic_color_map[color] = DEFAULT_UNMAPPED_BRUSH
                break
            elif user_input.isdigit():
                template_num = int(user_input)
                # UPDATED: Check range 1 to MAX_TEMPLATE_COLORS
                if 1 <= template_num <= MAX_TEMPLATE_COLORS:
                    avalonia_resource = f"{{StaticResource TemplateColor{template_num}}}"
                    dynamic_color_map[color] = avalonia_resource
                    print(f"  Mapping '{color}' -> TemplateColor{template_num}")
                    break
                else:
                    # UPDATED: Error message reflects new range
                    print(f"  Invalid number. Please enter 0 or a number between 1 and {MAX_TEMPLATE_COLORS}.")
            else:
                # UPDATED: Error message reflects new range and no 's'
                print(f"  Invalid input. Please enter 0 or a number between 1 and {MAX_TEMPLATE_COLORS}.")

    print("---------------------------------")
    print("Color mapping complete.")
    return dynamic_color_map

def get_avalonia_brush_attribute(svg_color, color_map):
    """Gets the Avalonia Brush attribute string based on the dynamic mapping."""
    normalized = normalize_color(svg_color)
    if not normalized or normalized == 'none':
        return "" # No brush for 'none' or empty

    if normalized in color_map:
        brush_value = color_map[normalized]
        # Check if it's a resource key or a direct color
        if brush_value.startswith("{") and brush_value.endswith("}"):
             return f'Brush="{brush_value}"' # e.g., Brush="{StaticResource TemplateColor1}"
        else:
             return f'Brush="{brush_value}"' # e.g., Brush="#FF0000" or Brush="Black"
    else:
        # This case should ideally not happen now without skip,
        # but as a fallback, use the default.
        print(f"Warning: Color '{svg_color}' (normalized: '{normalized}') was not found in the map. Using default '{DEFAULT_UNMAPPED_BRUSH}'.")
        return f'Brush="{DEFAULT_UNMAPPED_BRUSH}"'


# --- Core XAML Generation Function ---
# Needs to accept the dynamic_color_map
def generate_xaml_for_svg(svg_path, output_key, dynamic_color_map):
    """Parses an SVG and generates the Avalonia DrawingImage XAML using the dynamic map."""
    try:
        namespaces = {'svg': 'http://www.w3.org/2000/svg'}
        ET.register_namespace('', namespaces['svg'])
        tree = ET.parse(svg_path)
        root = tree.getroot()
    except ET.ParseError as e:
        print(f"Error parsing SVG file {svg_path}: {e}")
        return None
    except FileNotFoundError:
        print(f"Error: SVG file not found at {svg_path}")
        return None

    geometry_drawings = []
    # Combine search for different element types if needed (paths are most common)
    elements_to_process = root.findall('.//svg:path[@d]', namespaces)
    if not elements_to_process:
         elements_to_process = root.findall('.//path[@d]') # Try without namespace if needed

    # --- Add other shapes if necessary ---
    # elements_to_process.extend(root.findall('.//svg:rect', namespaces))
    # elements_to_process.extend(root.findall('.//svg:circle', namespaces))
    # ... you'd need to adapt geometry extraction for these ...

    if not elements_to_process:
         # Allow processing even if no paths, in case other shapes are added later
         # print(f"Warning: No <path> elements with 'd' attribute found in {svg_path}")
         pass # Continue to check for other potential elements if logic were added

    # --- Logic to handle other shapes (rect, circle, etc.) would go here ---
    # Example for rect (needs x, y, width, height extraction)
    # rects = root.findall('.//svg:rect', namespaces)
    # if not rects: rects = root.findall('.//rect')
    # elements_to_process.extend(rects)
    #
    # circles = root.findall('.//svg:circle', namespaces)
    # if not circles: circles = root.findall('.//circle')
    # elements_to_process.extend(circles)
    # --- End Example ---

    if not elements_to_process:
         print(f"Warning: No processable geometry elements (path, rect, circle, etc.) found in {svg_path}")
         return None # Return None if absolutely nothing was found

    indent = "            " # 12 spaces

    for element in elements_to_process:
        geometry_attribute = None
        # --- Geometry Extraction ---
        tag_name = element.tag.split('}')[-1] # Get tag name without namespace

        if tag_name == 'path':
            path_data = element.get('d')
            if not path_data: continue
            path_data = re.sub(r'\s+', ' ', path_data).strip()
            path_data_xaml = path_data.replace('"', '"') # Use XML entity for quotes in data
            geometry_attribute = f'Geometry="{path_data_xaml}"'

        # --- Add logic here to extract geometry for other shapes if needed ---
        # elif tag_name == 'rect':
        #     try:
        #         x = float(element.get('x', 0))
        #         y = float(element.get('y', 0))
        #         w = float(element.get('width'))
        #         h = float(element.get('height'))
        #         # Basic RectangleGeometry - Avalonia might prefer Path syntax for complex transforms
        #         geometry_attribute = f'Geometry="M{x},{y} L{x+w},{y} L{x+w},{y+h} L{x},{y+h} Z"'
        #     except (ValueError, TypeError, AttributeError) as e:
        #          print(f"Warning: Skipping rect in {svg_path} due to invalid attributes: {e}")
        #          continue
        # elif tag_name == 'circle':
        #      try:
        #         cx = float(element.get('cx'))
        #         cy = float(element.get('cy'))
        #         r = float(element.get('r'))
        #         if r <= 0: continue # Skip circles with no radius
        #         # EllipseGeometry for circle
        #         geometry_attribute = f'Geometry="M{cx-r},{cy} A{r},{r} 0 1 0 {cx+r},{cy} A{r},{r} 0 1 0 {cx-r},{cy} Z"'
        #      except (ValueError, TypeError, AttributeError) as e:
        #           print(f"Warning: Skipping circle in {svg_path} due to invalid attributes: {e}")
        #           continue
        # Add elif for ellipse, polygon, polyline etc. if required

        else:
            # Only warn if it's an element we *might* have expected (like if we added rect logic but it failed)
            # Or just ignore unknown elements silently. Let's ignore for now.
            # print(f"Warning: Skipping unsupported element type '{tag_name}' in {svg_path}")
             continue

        if not geometry_attribute: # Skip if geometry couldn't be extracted
            continue

        # --- Style Extraction (Handles fill, stroke, stroke-width from attributes) ---
        # TODO: Add parsing for 'style' attribute if needed (more complex)
        fill_color = element.get('fill')
        stroke_color = element.get('stroke')
        stroke_width = element.get('stroke-width')

        # Get Fill Brush
        brush_attribute = get_avalonia_brush_attribute(fill_color, dynamic_color_map)
        brush_attribute_spaced = f" {brush_attribute}" if brush_attribute else ""

        # Get Pen (Stroke)
        pen_xaml = ""
        normalized_stroke = normalize_color(stroke_color)
        if normalized_stroke and normalized_stroke != 'none' and stroke_width:
            try:
                thickness = float(stroke_width)
                if thickness <= 0: thickness = 1.0 # Default thickness if 0 or negative
            except (ValueError, TypeError):
                 print(f"Warning: Invalid stroke-width '{stroke_width}' in {svg_path}. Using default 1.")
                 thickness = 1.0

            pen_brush_attribute = get_avalonia_brush_attribute(stroke_color, dynamic_color_map)

            if pen_brush_attribute:
                pen_brush_value = pen_brush_attribute.split('=', 1)[1].strip('"')
                pen_xaml = f"""
{indent}    <GeometryDrawing.Pen>
{indent}        <Pen Thickness="{thickness}" Brush="{pen_brush_value}"/>
{indent}    </GeometryDrawing.Pen>"""
            else:
                 # This shouldn't happen easily without the 'skip' option, but good fallback.
                 print(f"Warning: Could not determine Pen brush for stroke '{stroke_color}' in {svg_path}. Stroke ignored.")


        # Assemble Drawing XAML
        drawing_xaml = f"""
{indent}<GeometryDrawing{brush_attribute_spaced}
{indent}                 {geometry_attribute}>{pen_xaml}
{indent}</GeometryDrawing>"""
        geometry_drawings.append(drawing_xaml)

    if not geometry_drawings:
        print(f"Warning: No drawable geometry found or converted for {svg_path}")
        return None

    # Assemble the final DrawingImage
    xaml_template = f"""
    <DrawingImage x:Key="{output_key}">
        <DrawingGroup>{"".join(geometry_drawings)}
        </DrawingGroup>
    </DrawingImage>"""
    return xaml_template


# --- Helper Function (sanitize_key - unchanged) ---
def sanitize_key(filename):
    """Creates a valid PascalCase XAML key from a filename."""
    base = os.path.splitext(filename)[0]
    # Remove invalid characters, replace separators with space for title casing later
    key = re.sub(r'[^\w\s-]', '', base)  # Allow word chars, space, hyphen
    key = re.sub(r'[-\s]+', ' ', key).strip()  # Replace separators with single space

    # Smart title casing without forcing lowercase
    parts = key.split(' ')
    key = "".join(part[:1].upper() + part[1:] for part in parts if part)

    # Ensure it doesn't start with a number
    if key and key[0].isdigit():
        key = "_" + key
    # Ensure it's not empty
    if not key:
        key = "UnnamedIcon"
    return key

# --- Function to generate Preview XAML (unchanged) ---
def generate_preview_xaml(resource_keys):
    """Generates the Design.PreviewWith XAML block."""
    if not resource_keys:
        return ""

    items_indent = "                " # 16 spaces
    preview_items = []
    # Take only the first MAX_PREVIEW_ITEMS for the preview
    for key in resource_keys[:MAX_PREVIEW_ITEMS]:
        # Use specific Neutral colors as requested
        item_xaml = f"""
{items_indent}<util:MultiColorExampleComponent IconData="{{DynamicResource {key}}}" IconName="{key}"
{items_indent}                                 Color1="{{StaticResource Neutral50}}"
{items_indent}                                 Color2="{{StaticResource Neutral300}}"
{items_indent}                                 Color3="{{StaticResource Neutral600}}"
{items_indent}                                 Color4="{{StaticResource Neutral800}}"
{items_indent}                                 Color5="{{StaticResource Neutral950}}"
{items_indent}/>""" # Removed Width/Height
        # Note: This preview component only shows 5 colors. If you map to TemplateColor6-12,
        # they won't be visualized here unless the MultiColorExampleComponent is updated.
        preview_items.append(item_xaml)

    # Removed Width from UniformGrid, Updated Border Background
    preview_xaml = f"""
    <Design.PreviewWith>
        <Border Padding="20" Background="{{StaticResource Neutral400}}">
            <UniformGrid Columns="5">{"".join(preview_items)}
            </UniformGrid>
        </Border>
    </Design.PreviewWith>
"""
    return preview_xaml

# --- Main Execution ---
if __name__ == "__main__":

    input_folder = ""
    while not os.path.isdir(input_folder):
        input_folder = input("Please enter the path to the folder containing SVG files: ").strip()
        if not os.path.isdir(input_folder):
            print(f"Error: '{input_folder}' is not a valid directory. Please try again.")

    # --- 1. Find Unique Colors ---
    unique_svg_colors = find_unique_colors_in_svgs(input_folder)

    # --- 2. Build Color Map Interactively ---
    # Pass the sorted list of unique colors
    interactive_color_map = build_interactive_color_map(unique_svg_colors)

    # --- Prepare Output Path ---
    folder_name = os.path.basename(os.path.normpath(input_folder))
    parent_dir = os.path.dirname(input_folder) if os.path.dirname(input_folder) else '.' # Handle case where input is just folder name
    output_filename = f"{folder_name}.axaml"
    output_path = os.path.join(parent_dir, output_filename)

    print(f"\nProcessing SVGs in: {input_folder}")
    print(f"Output will be written to: {output_path}")

    all_xaml_outputs = []
    generated_keys = []

    svg_files = [f for f in os.listdir(input_folder) if f.lower().endswith('.svg')]

    if not svg_files:
        print("No SVG files found in the directory.")
        exit(0)

    svg_files.sort() # Ensure consistent processing order

    # --- 3. Generate XAML using the Interactive Map ---
    for filename in svg_files:
        input_path = os.path.join(input_folder, filename)
        output_key = sanitize_key(filename)
        print(f"  Processing {filename} -> Key: {output_key}")
        # Pass the created interactive_color_map to the generation function
        xaml_output = generate_xaml_for_svg(input_path, output_key, interactive_color_map)
        if xaml_output:
            all_xaml_outputs.append(xaml_output)
            generated_keys.append(output_key)

    if not all_xaml_outputs:
        print("No valid XAML generated.")
        exit(0)

    # --- 4. Generate Preview and Final Output ---
    preview_block = generate_preview_xaml(generated_keys)
    joined_icon_xaml = "\n".join(all_xaml_outputs)

    final_xaml = f"""<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:util="{PREVIEW_NAMESPACE_UTIL}">

    <!-- Generated by svg_to_avalonia.py from folder: {folder_name} -->
    <!-- Color mappings were defined interactively during script execution. -->
{preview_block}
{joined_icon_xaml}
</ResourceDictionary>
"""

    try:
        output_dir_check = os.path.dirname(output_path)
        # Create output directory if it doesn't exist and is not the current directory
        if output_dir_check and not os.path.exists(output_dir_check):
             print(f"Creating output directory: {output_dir_check}")
             os.makedirs(output_dir_check)

        with open(output_path, 'w', encoding='utf-8') as f:
            f.write(final_xaml)
        print(f"\nSuccessfully wrote Avalonia ResourceDictionary to: {output_path}")
    except IOError as e:
        print(f"\nError writing output file {output_path}: {e}")
    except OSError as e:
         print(f"\nError creating output directory for {output_path}: {e}")