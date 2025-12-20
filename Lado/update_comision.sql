UPDATE AspNetUsers SET ComisionRetiro = 20 WHERE ComisionRetiro = 0 OR ComisionRetiro IS NULL;
UPDATE AspNetUsers SET MontoMinimoRetiro = 50 WHERE MontoMinimoRetiro = 0 OR MontoMinimoRetiro IS NULL;
