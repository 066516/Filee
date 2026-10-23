# Filee no GNOME Wayland

Registro de implementação e guia de teste — 23/10/2026.

## Como usar

1. Instale esta versão do Filee com `./install.sh`. No GNOME, o pacote inclui a extensão **Filee donut at the cursor**.
2. Abra **Extensões** e ative essa extensão. Se ela não aparecer ou ainda carregar uma versão anterior, **encerre a sessão e entre novamente**. Reiniciar apenas o Filee não recarrega o código da extensão no GNOME Shell.
3. Abra o Filee. Em **Atalhos**, a detecção deve aparecer em execução. Se aparecer “Aguardando a extensão do Filee para GNOME”, consulte as verificações abaixo.
4. Em **Arquivos / Nautilus**, segure **Ctrl**, clique em um arquivo e arraste. Quando a roda aparecer, mova o arquivo até o formato desejado e solte.
5. Para interromper os gestos, use **Pausar** no Filee ou desative a extensão. Os modificadores personalizados e os aplicativos excluídos continuam sendo considerados.

**Não é necessário executar o Filee como administrador, usar sudo ou entrar no grupo `input`.** A extensão fornece somente o estado do ponteiro e das teclas modificadoras; não lê texto digitado nem o conteúdo dos arquivos.

A integração depende do GNOME Shell e funciona com o aplicativo do Filee em XWayland, como a versão atual do Avalonia. Não adiciona um backend gráfico nativo Wayland ao Avalonia. O caminho do KDE continua independente.

## Por que Ctrl + arrastar fazia uma cópia

A versão anterior habilitava o GNOME no mesmo caminho do KDE, que usa libinput para ler `/dev/input`. A sessão examinada não tinha permissão para essa leitura. A extensão estava ativa, mas só tentava posicionar uma janela: ela não fornecia eventos para o detector de gestos. Assim, a roda nunca abria e o Nautilus executava sua ação normal de copiar com Ctrl.

Também havia um problema de posicionamento: a extensão recentralizava continuamente a roda no ponteiro. Isso impediria alcançar uma fatia para escolher o formato. Agora ela posiciona a roda **uma vez por abertura**, mantendo-a parada até o próximo gesto.

Esta implementação não desativa a função de cópia do Nautilus. O Filee oferece um destino para a soltura quando detecta o gesto e recebe um arraste de arquivos. Com a extensão desativada, o Filee pausado, gesto desabilitado, aplicativo excluído ou soltura fora da roda, o gerenciador mantém seu comportamento normal. Uma seleção sem arquivos não deve exibir formatos.

## O que mudou

- **Extensão GNOME:** detecta estado global do ponteiro com `Shell.Global.get_pointer`, identifica o aplicativo sob ele e fornece snapshots pelo socket privado `$XDG_RUNTIME_DIR/filee-gnome/pointer-v1.sock`.
- **Permissões:** diretório privado do usuário, socket `0600` e validação das credenciais do cliente. Nenhum acesso a `/dev/input` nem registro de teclas digitadas.
- **Comunicação:** escritas assíncronas, sem fila ilimitada; um consumidor lento não bloqueia o desktop. O aplicativo reconecta quando a extensão volta e encerra um gesto se a comunicação cai. Bloqueio de tela, visão geral e diálogos modais suspendem os snapshots de entrada.
- **Detecção no Filee:** um adaptador transforma snapshots em eventos para o `GestureDetector` existente. Pausa, botões, limiar de movimento, modificadores, escopo e exclusões passam pelo mesmo detector. Não carrega SharpHook no caminho GNOME.
- **Posicionamento:** limita a janela ao monitor do ponteiro e não rouba o foco. Exige título e identidade de aplicativo do Filee antes de mover uma janela.
- **Interface:** informa que está aguardando a extensão e explica a ativação em português e inglês. Os demais idiomas recebem os novos textos em inglês, preservando as traduções já existentes.
- **Pacote:** inclui a extensão e este guia; o instalador preserva a lista de outras extensões e o desinstalador remove apenas a extensão marcada como gerenciada pelo Filee.

O GNOME tenta conectar à extensão por padrão; ativá-la é a opção de participação. `FILEE_WAYLAND_GESTURES=0` permite desativar explicitamente a ponte no processo. O modo experimental KDE mantém sua opção `FILEE_WAYLAND_GESTURES=1`, libinput e script KWin. Não foram alterados conversores, motores, formatos, configurações de conversão, implementações Windows/macOS ou o detector compartilhado de gestos.

## Se a roda não abrir

- Confira se **Pausar** está desligado e se o gesto **Ctrl + botão esquerdo + arrastar** está habilitado em **Atalhos**.
- Confira a extensão no aplicativo **Extensões**. Uma versão antiga pode aparecer ativa sem fornecer a nova ponte. Saia da sessão e entre novamente após atualizá-la.
- O **Arquivos / Nautilus** não deve estar na lista de aplicativos excluídos.
- Confira se o processo não foi iniciado com `FILEE_WAYLAND_GESTURES=0`.
- Não execute o aplicativo com `sudo`: a ponte pertence ao usuário da sessão gráfica.
- A área de soltar e o menu do gerenciador permanecem utilizáveis enquanto a extensão não está disponível.

Verificações opcionais no Terminal:

```sh
gnome-extensions info filee-donut@filee.sh
ls -l "$XDG_RUNTIME_DIR/filee-gnome/pointer-v1.sock"
```

Com a nova extensão ativa, o segundo comando deve mostrar um socket acessível somente ao usuário. O log do Filee registra `GNOME pointer extension connected` quando a conexão funciona. A ausência desse registro não comprova que o gesto tenha sido detectado: confira também o estado da extensão e o socket.

## Limites explícitos

- Integração experimental; metadados permitem GNOME Shell 45–50. Testes com bibliotecas reais foram executados no Fedora 44 / GNOME 50; não se afirma validação de todas essas versões.
- Não implementa atalhos globais para consultar seleção nem gestos de segurar. O caminho Linux já não oferecia esses recursos de seleção global.
- A ponte não fornece Tab ou roda do mouse globalmente para alternar faces durante um arraste. Não se afirma funcionamento dessa alternância em outros clientes durante drag-and-drop.
- O estado do ponteiro é amostrado a cada 16 ms enquanto o Filee está conectado; cliques muito curtos entre amostras podem não ser observados.
- O escopo usa a geometria e ordem de janelas visíveis; não interpreta regiões internas como barra de tarefas/painéis de outros clientes. Processos que desapareçam antes da consulta podem ficar sem identificação.
- Captura visual, posicionamento com escalas diferentes e entrega de drag-and-drop entre Nautilus nativo Wayland e Filee em XWayland exigem teste real na sessão. Testes automatizados não substituem essa confirmação.

## Testes de regressão e reprodução

```sh
dotnet test tests/Filee.App.Tests/Filee.App.Tests.csproj -c Release --filter FullyQualifiedName~GnomePointerBridgeTests
dotnet test Filee.slnx -c Release
node --check installer/linux/gnome/filee-donut@filee.sh/extension.js
sh -n installer/linux/install.sh installer/linux/uninstall.sh
```

O teste GJS exercita o **socket Gio real** com janelas e ponteiro simulados, sem alterar o desktop em uso. Em Fedora/GNOME 50 (ajuste os caminhos para a versão do Mutter instalada):

```sh
filee_runtime_test=$(mktemp -d /tmp/filee-gjs-XXXXXX)
XDG_RUNTIME_DIR="$filee_runtime_test" \
GI_TYPELIB_PATH=/usr/lib64/mutter-18 \
LD_LIBRARY_PATH=/usr/lib64/mutter-18 \
gjs tests/gnome/extension-smoke.js "$PWD/installer/linux/gnome/filee-donut@filee.sh/extension.js"
```

Cobertura nova: Ctrl + arrastar com limiar, soltura única, arraste sem Ctrl, escopo rejeitado, reconexão durante um arraste, bloqueio/desbloqueio, pausa, gesto desabilitado, modificadores personalizados e tardios, validação do protocolo, comunicação Unix real e reconexão após fechamento. No lado GJS: permissões privadas, máscaras de botões/modificadores, bloqueio de tela, posição fixa por abertura, bordas do monitor e limpeza ao desativar.

### Roteiro manual obrigatório

Use um arquivo descartável em uma pasta temporária:

- [ ] Ctrl + arrastar no Nautilus abre a roda; soltar sobre um formato inicia a conversão e não cria uma cópia do arquivo de origem no local da soltura.
- [ ] Mover o ponteiro alcança as fatias; a roda não foge do cursor.
- [ ] Soltar/cancelar sem escolher um formato fecha a roda e preserva o arquivo original.
- [ ] Arrastar sem Ctrl mantém a operação normal do Nautilus.
- [ ] Pausar ou desabilitar o gesto impede a abertura.
- [ ] Modificador personalizado funciona; aplicativo excluído não abre a roda.
- [ ] Desativar/reativar a extensão desconecta/reconecta sem reiniciar o Filee.
- [ ] Bloquear/desbloquear a tela não deixa um gesto preso.
- [ ] Testar monitor secundário, escala fracionária e bordas de tela.
- [ ] Conferir área de soltar, menu de contexto e uma conversão existente.
- [ ] Em X11/KDE, repetir o gesto existente no ambiente correspondente.

### Resultado nesta execução

- **11 testes novos da ponte:** aprovados.
- **110 testes direcionados do aplicativo:** aprovados, incluindo ponte GNOME, ciclo de vida do hook, detector, controlador da roda e traduções. A primeira execução geral encontrou ausência das duas novas chaves em outros idiomas; as chaves foram adicionadas e essa verificação passou na repetição direcionada.
- **Núcleo:** 513 aprovados. **CLI:** 28 aprovados. **Plataformas:** 52 aprovados e 5 ignorados por plataforma indisponível.
- **GJS com Gio real:** aprovado, incluindo permissões, comunicação, posicionamento fixo, bordas e limpeza.
- **GNOME Shell 50 real, em sessão headless isolada:** extensão carregada; Filee conectado sem grupo `input`; entrada virtual Ctrl + arrastar em um cliente GTK4 Wayland nativo abriu a roda do Filee em XWayland. Soltar sobre uma fatia executou `to-pdf` e produziu `sample.pdf`, preservando `sample.png` byte a byte e sem arquivo de cópia adicional. O cliente GTK4 de teste identificou seu processo como `nautilus` para exercitar o escopo padrão; **não foi um teste manual dentro do Nautilus**.
- **Compatibilidade X11:** inicialização contra o XWayland da sessão isolada, com ambiente X11, carregou o hook existente (`Global input hook started`), sem selecionar a ponte GNOME. Não substitui teste em uma sessão Xorg/KDE real.
- **Publicação Linux x64:** aplicativo e CLI compilados como `2.0.0-gnome.2`; CLI confirmou a versão; aplicativo instalado e iniciado. Fontes mantêm a versão original do projeto, sem alteração de versionamento para o PR.
- **Instalação anterior:** preservada em `artifacts/gnome.2/previous-installed-gnome.1`, incluindo a extensão anterior. Configurações e motores do usuário foram preservados. A extensão nova está no disco; a sessão GNOME atual ainda mantém o módulo anterior em memória, portanto é necessário sair e entrar novamente.
- **Suíte geral:** não ficou totalmente verde neste ambiente. Setup teve 36 falhas; quatro falhas representativas de caminhos/recursos Windows também foram reproduzidas no código original `098b02a`, sem o patch. Engines teve 70 falhas, incluindo FFmpeg sem `libx264`, conversões LibreOffice e expectativa de seleção de engine. Esse código foi mantido intacto. A longa bateria de renderização do aplicativo foi interrompida após aproximadamente 15 minutos, ainda produzindo capturas; não se declara aprovação integral dessa bateria nem da suíte geral. Os 110 testes direcionados foram concluídos separadamente após o ajuste das traduções.
- **Validação restante:** Nautilus na sessão do usuário, múltiplos monitores/escala fracionária e outras versões GNOME/KDE/Xorg. Os itens manuais acima permanecem desmarcados.

Os registros desta execução e o par PNG/PDF de teste ficam localmente em `artifacts/gnome.2/validation` (pasta ignorada pelo Git).

## Referências técnicas

- [GNOME Shell: `Global.get_pointer`](https://gnome.pages.gitlab.gnome.org/gnome-shell/shell/method.Global.get_pointer.html).
- [Mutter: `Window.move_frame`](https://mutter.gnome.org/meta/method.Window.move_frame.html).
- [Gio: `SocketService::incoming`](https://gnome.pages.gitlab.gnome.org/gtk/gio/signal.SocketService.incoming.html).
