## Flexibilize

### versão
    1.0.0

### Rodando o Flexibilize

#### Passo 1: Build a imagem
    $ docker build -t ponto-eletronico .

#### Passo 2: Criar um Script para Executar o Docker
    crie um arquivo chamado run_ponto_eletronico.sh (exemplo)

    #!/bin/bash
    # Executa o contêiner Docker com o sistema de bate ponto
    docker run --rm ponto-eletronico

#### Passo 3: Colocando o script executavel

    $ chmod +x run_ponto_eletronico.sh

#### Passo 4: Configurar o Cron Job para Rodar o Script
Agora, você pode configurar o cron job para executar o script run_ponto_eletronico.sh nas 8:00 AM e às 15:00 PM todos os dias. Para isso, edite o crontab do seu usuário no Linux

    $ crontab -e

    0 8 * * * /caminho/para/o/script/run_ponto_eletronico.sh
    0 15 * * * /caminho/para/o/script/run_ponto_eletronico.sh

Verificar se o Cron Está em Execução
    
    # sudo service cron status

senão estiver em execução, inicie-o com:

    $ sudo service cron start
