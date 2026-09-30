# PDF-Compressor-UG

## Requisitos e Dependências Externas

### Ghostscript
Este software utiliza localmente o **Ghostscript** (binário `gswin64c.exe` no Windows) como motor de reprocessamento e compressão de arquivos PDF.

- **Versão recomendada/homologada:** **GPL Ghostscript 10.08.0** (ou superior da série 10.x, 64-bit).
- **Site oficial:** [ghostscript.com](https://ghostscript.com/index.html)
- **Download oficial:** Disponível em [ghostscript.com/releases.html](https://ghostscript.com/releases.html) (selecionar *Ghostscript AGPL release for Windows (64 bit)* — ex.: `gs10080w64.exe`).
- **Modo de distribuição:** Dependência externa autônoma (não embutida no executável da aplicação para manter o isolamento de licenças **AGPL**).
- **Detecção automática:** A aplicação busca o executável automaticamente em:
  1. `C:\Program Files\gs\gs*\bin\gswin64c.exe` (detecta pastas como `gs10.08.0`, ordenando pela versão mais recente instalada);
  2. Registro do Windows (`HKLM` / `HKCU\SOFTWARE\Artifex\Ghostscript`);
  3. Variável de ambiente `PATH`.

---

## Documentação do projeto

- [Levantamento de necessidades](levantamento-necessidades-pdf-compressor.md)
- [Plano de desenvolvimento por etapas](docs/plano-pdf-compressor/00-indice.md)
- [Validação no Windows (Etapa 8)](docs/plano-pdf-compressor/09-validacao-windows.md)
