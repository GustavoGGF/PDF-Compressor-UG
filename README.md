# PDF Compressor

<p align="center">
  <img src="src/PdfCompressor/Assets/logo.png" alt="Logo do PDF Compressor" width="220">
</p>

<p align="center">
  Comprima arquivos PDF localmente no Windows, com controle de qualidade e tamanho final.
</p>

## Sobre o projeto

O **PDF Compressor** é um aplicativo desktop para reduzir o tamanho de arquivos PDF sem enviar os documentos para a internet.

Ele analisa o arquivo selecionado, aplica uma estratégia de compressão com o Ghostscript e gera uma cópia compactada. O arquivo original não é sobrescrito.

## Funcionalidades

- Seleção de um arquivo PDF pelo explorador de arquivos.
- Arrastar e soltar um PDF na janela do aplicativo.
- Análise do tamanho e da quantidade de páginas antes da compressão.
- Modo automático com busca por tamanho-alvo.
- Presets de qualidade para diferentes necessidades:
  - **Alta qualidade** — 300 DPI, indicado para impressão.
  - **Qualidade média** — 150 DPI, indicado para leitura em tela.
  - **Compressão máxima** — 72 DPI, priorizando o menor tamanho.
- Definição do tamanho-alvo em MB ou KB no modo automático.
- Cancelamento do processamento em andamento.
- Exibição do tamanho original, tamanho final, redução obtida e qualidade aplicada.
- Abertura do PDF gerado ou da pasta de saída ao finalizar.
- Aviso quando o PDF possui uma assinatura que pode ser invalidada pelo reprocessamento.

## Requisitos

- Windows 10 ou superior, 64 bits.
- [Ghostscript AGPL para Windows 64 bits](https://ghostscript.com/releases/).
- Versão recomendada e homologada: **GPL Ghostscript 10.08.0** ou superior da série 10.x.

O aplicativo utiliza o executável `gswin64c.exe` como motor de compressão. A instalação do Ghostscript é separada do aplicativo por causa dos termos de distribuição da licença AGPL.

Após a instalação, o PDF Compressor procura automaticamente o Ghostscript em:

1. `C:\Program Files\gs\gs*\bin\gswin64c.exe`;
2. Registro do Windows (`HKLM` e `HKCU`);
3. Variável de ambiente `PATH`.

## Download e instalação

Baixe a versão mais recente na página de [Releases](https://github.com/GustavoGGF/PDF-Compressor-UG/releases) e extraia o aplicativo em uma pasta de sua preferência.

Depois:

1. Instale o Ghostscript 64 bits.
2. Abra o `PdfCompressor.exe`.
3. Se o Ghostscript estiver instalado em um dos locais reconhecidos, o aplicativo estará pronto para uso.

> O aplicativo é publicado para `win-x64` como executável autocontido. O .NET Runtime não precisa ser instalado separadamente.

## Como usar

1. Abra o PDF Compressor.
2. Clique em **Selecionar PDF...** ou arraste um arquivo PDF para a área indicada.
3. Aguarde a análise do arquivo.
4. Escolha um modo de compressão:
   - Use **Automático** para informar um tamanho-alvo em MB ou KB.
   - Use um preset de qualidade quando preferir controlar diretamente o equilíbrio entre qualidade e tamanho.
5. Clique em **Comprimir PDF**.
6. Ao terminar, confira o resultado e use **Abrir PDF** ou **Abrir Pasta**.

O arquivo compactado é criado na mesma pasta do arquivo original, com um novo nome. O PDF de entrada permanece preservado.

## Limitações e cuidados

- O aplicativo processa um arquivo PDF individual por vez; diretórios não são aceitos.
- PDFs inválidos, inacessíveis ou protegidos por senha podem não ser processados.
- A compressão pode reduzir a resolução de imagens e alterar a qualidade visual do documento.
- O tamanho-alvo é uma tentativa orientada pelo conteúdo do PDF. Alguns documentos podem não alcançar o tamanho solicitado; nesses casos, o aplicativo informa o resultado de melhor esforço.
- Reprocessar um PDF assinado digitalmente pode invalidar sua assinatura. O arquivo original não é alterado.
- O resultado deve ser conferido antes de substituir ou compartilhar o documento original.

## Privacidade

O processamento é realizado localmente no computador do usuário. Os arquivos PDF não precisam ser enviados para um serviço online.

## Compilar a partir do código-fonte

Para desenvolver ou compilar o projeto, instale o [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) e execute:

```powershell
dotnet restore
dotnet build PdfCompressor.sln -c Release
```

Para publicar a versão Windows autocontida:

```powershell
dotnet publish src/PdfCompressor/PdfCompressor.csproj `
  -c Release `
  -f net8.0-windows `
  -r win-x64 `
  --self-contained
```

Os testes automatizados podem ser executados com:

```powershell
dotnet test tests/PdfCompressor.Tests/PdfCompressor.Tests.csproj
```

## Licença

Consulte os arquivos e avisos de licença distribuídos com o projeto e com o Ghostscript. O Ghostscript é uma dependência externa licenciada sob AGPL.