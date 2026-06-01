"""Генерує app.ico для десктоп-додатка — та сама іконка, що й web/public/favicon.svg
(КП-прапорець на індиго). Рисуємо примітивами Pillow із суперсемплінгом ×4, бо
SVG-рендерера в середовищі немає. Запуск: python make_icon.py"""
from PIL import Image, ImageDraw

S = 256          # базовий розмір кадру
SS = 4           # суперсемплінг для згладжування
W = S * SS

BG_TOP = (61, 66, 200)     # #3d42c8
BG_BOT = (34, 42, 146)     # #222a92
WHITE  = (238, 240, 251)   # #eef0fb
ORANGE = (238, 123, 46)    # #ee7b2e
PURE   = (255, 255, 255)


def lerp(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))


def rounded_mask(size, radius):
    m = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(m)
    d.rounded_rectangle([0, 0, size - 1, size - 1], radius=radius, fill=255)
    return m


img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
draw = ImageDraw.Draw(img)

# Вертикальний градієнт-підкладка
for y in range(W):
    draw.line([(0, y), (W, y)], fill=lerp(BG_TOP, BG_BOT, y / (W - 1)))

# Обрізаємо в заокруглений квадрат (rx=14 у 64-в'юпорті → 14/64 * S)
mask = rounded_mask(W, int(round(14 / 64 * W)))
img.putalpha(mask)

draw = ImageDraw.Draw(img)


def px(v):
    """Координати з 64-в'юпорту SVG у пікселі поточного кадру."""
    return v / 64 * W


# Стійка прапорця (x=20, y=16, w=3.4, h=34)
draw.rounded_rectangle(
    [px(20), px(16), px(20 + 3.4), px(16 + 34)],
    radius=px(1.7), fill=WHITE,
)

# КП-прапорець: квадрат 23.4..44 / 16..36, діагональ ділить білий|помаранчевий
x0, y0, x1, y1 = px(23.4), px(16), px(44), px(36)
# Білий трикутник (лівий-нижній), помаранчевий (правий-верхній)
draw.polygon([(x0, y0), (x0, y1), (x1, y1)], fill=PURE)
draw.polygon([(x0, y0), (x1, y0), (x1, y1)], fill=ORANGE)
# Тонка біла рамка квадрата
draw.rectangle([x0, y0, x1, y1], outline=PURE, width=max(1, int(px(1.2))))

# Помаранчева підставка (circle cx=21.7 cy=50 r=4.2)
cx, cy, r = px(21.7), px(50), px(4.2)
draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=ORANGE)

# Даунсемпл до базового розміру, далі — у набір розмірів .ico
base = img.resize((S, S), Image.LANCZOS)
sizes = [16, 24, 32, 48, 64, 128, 256]
base.save("app.ico", format="ICO", sizes=[(s, s) for s in sizes])
print("written app.ico with sizes", sizes)
