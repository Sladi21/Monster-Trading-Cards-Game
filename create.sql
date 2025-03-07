CREATE TABLE users (
                       username TEXT PRIMARY KEY,
                       password_hash TEXT NOT NULL,
                       salt TEXT NOT NULL,
                       coins INT DEFAULT 0,
                       wins INT DEFAULT 0,
                       losses INT DEFAULT 0,
                       elo INT DEFAULT 1000,
                       token TEXT,
                       player_cards JSONB,
                       player_deck JSONB
);

CREATE TABLE cards (
                     id SERIAL PRIMARY KEY,
                       name TEXT NOT NULL,
                       damage DOUBLE PRECISION,
                       element CHARACTER VARYING(50),
                       card_type CHARACTER VARYING(50)
);

INSERT INTO cards (name, damage, id, element, card_type)
VALUES
    ('WaterGoblin', 55, 1, 'Water', 'Monster'),
    ('FireGoblin', 50, 2, 'Fire', 'Monster'),
    ('RegularGoblin', 45, 3, 'Normal', 'Monster'),
    ('WaterTroll', 60, 4, 'Water', 'Monster'),
    ('FireTroll', 65, 5, 'Fire', 'Monster'),
    ('RegularTroll', 55, 6, 'Normal', 'Monster'),
    ('WaterElf', 40, 7, 'Water', 'Monster'),
    ('FireElf', 45, 8, 'Fire', 'Monster'),
    ('RegularElf', 42, 9, 'Normal', 'Monster'),
    ('WaterSpell', 70, 10, 'Water', 'Spell'),
    ('FireSpell', 75, 11, 'Fire', 'Spell'),
    ('RegularSpell', 65, 12, 'Normal', 'Spell'),
    ('Knight', 80, 13, 'Normal', 'Monster'),
    ('Dragon', 100, 14, 'Normal', 'Monster'),
    ('Ork', 85, 15, 'Normal', 'Monster'),
    ('Kraken', 90, 16, 'Normal', 'Monster'),
    ('Wizzard', 88, 17, 'Normal', 'Monster');