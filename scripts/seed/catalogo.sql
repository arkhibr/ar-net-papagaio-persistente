-- Seed do Catálogo (dados de referência para desenvolvimento e teste manual).
--
-- Uso (com a Api já tendo subido uma vez em Development, para o EnsureCreated criar o schema;
-- um catalogo.db anterior à tabela Equipes precisa ser apagado antes):
--   sqlite3 src/Api/catalogo.db < scripts/seed/catalogo.sql
--
-- Pode rodar de novo: apaga e recria só as linhas com os ids deste arquivo.
-- Ids em maiúsculas, como o EF Core grava Guid no SQLite.
--
-- Técnico e supervisor só enxergam a fila da equipe se estiverem em MembrosDeEquipe (um usuário
-- pertence a no máximo uma equipe). Depois do seed, tudo isto também pode ser mantido pela API de
-- administração do Catálogo (papel Supervisor; ver contratos-front.md).
--
-- Equipes
--   Infraestrutura e Redes   E0000000-0000-0000-0000-000000000001
--   Suporte ao Usuário       E0000000-0000-0000-0000-000000000002
--   Sistemas Corporativos    E0000000-0000-0000-0000-000000000003
--
-- Membros (use estes userId no /dev/login ou nas personas da página /auth/login)
--   Técnico Infra 1          A0000000-0000-0000-0000-000000000011  papel Tecnico
--   Técnico Infra 2          A0000000-0000-0000-0000-000000000012  papel Tecnico
--   Supervisor Infra         A0000000-0000-0000-0000-000000000019  papel Supervisor
--   Técnico Suporte 1        A0000000-0000-0000-0000-000000000021  papel Tecnico
--   Técnico Suporte 2        A0000000-0000-0000-0000-000000000022  papel Tecnico
--   Técnico Sistemas 1       A0000000-0000-0000-0000-000000000031  papel Tecnico
--
-- Solicitantes não precisam de linha no Catálogo; sugestão de persona:
--   Solicitante 1            A0000000-0000-0000-0000-000000000001  papel Solicitante
--   Solicitante 2            A0000000-0000-0000-0000-000000000002  papel Solicitante
--
-- SLA em horas por prioridade (0=Baixa, 1=Media, 2=Alta, 3=Critica). A categoria
-- "Acesso a sistema legado" não tem SLA para Crítica, de propósito: abrir um chamado Crítico
-- nela responde 400 de regra de negócio (M13 de achados.md).

BEGIN TRANSACTION;

DELETE FROM SlasDeCategoria WHERE CategoriaId LIKE 'C0000000-0000-0000-0000-0000000000__';
DELETE FROM CategoriasDeServico WHERE Id LIKE 'C0000000-0000-0000-0000-0000000000__';
DELETE FROM MembrosDeEquipe WHERE TecnicoId LIKE 'A0000000-0000-0000-0000-0000000000__';
DELETE FROM Equipes WHERE Id LIKE 'E0000000-0000-0000-0000-0000000000__';

INSERT INTO Equipes (Id, Nome) VALUES
  ('E0000000-0000-0000-0000-000000000001', 'Infraestrutura e Redes'),
  ('E0000000-0000-0000-0000-000000000002', 'Suporte ao Usuário'),
  ('E0000000-0000-0000-0000-000000000003', 'Sistemas Corporativos');

INSERT INTO CategoriasDeServico (Id, Nome, EquipeId, Ativa) VALUES
  ('C0000000-0000-0000-0000-000000000001', 'Rede e conectividade',       'E0000000-0000-0000-0000-000000000001', 1),
  ('C0000000-0000-0000-0000-000000000002', 'Servidores e armazenamento', 'E0000000-0000-0000-0000-000000000001', 1),
  ('C0000000-0000-0000-0000-000000000003', 'Estação de trabalho',        'E0000000-0000-0000-0000-000000000002', 1),
  ('C0000000-0000-0000-0000-000000000004', 'Impressoras e periféricos',  'E0000000-0000-0000-0000-000000000002', 1),
  ('C0000000-0000-0000-0000-000000000005', 'E-mail e colaboração',       'E0000000-0000-0000-0000-000000000002', 1),
  ('C0000000-0000-0000-0000-000000000006', 'ERP e sistemas internos',    'E0000000-0000-0000-0000-000000000003', 1),
  ('C0000000-0000-0000-0000-000000000007', 'Acesso a sistema legado',    'E0000000-0000-0000-0000-000000000003', 1);

INSERT INTO SlasDeCategoria (CategoriaId, Prioridade, Horas) VALUES
  -- Rede e conectividade
  ('C0000000-0000-0000-0000-000000000001', 0, 48), ('C0000000-0000-0000-0000-000000000001', 1, 16),
  ('C0000000-0000-0000-0000-000000000001', 2, 4),  ('C0000000-0000-0000-0000-000000000001', 3, 1),
  -- Servidores e armazenamento
  ('C0000000-0000-0000-0000-000000000002', 0, 72), ('C0000000-0000-0000-0000-000000000002', 1, 24),
  ('C0000000-0000-0000-0000-000000000002', 2, 8),  ('C0000000-0000-0000-0000-000000000002', 3, 2),
  -- Estação de trabalho
  ('C0000000-0000-0000-0000-000000000003', 0, 72), ('C0000000-0000-0000-0000-000000000003', 1, 24),
  ('C0000000-0000-0000-0000-000000000003', 2, 8),  ('C0000000-0000-0000-0000-000000000003', 3, 4),
  -- Impressoras e periféricos
  ('C0000000-0000-0000-0000-000000000004', 0, 96), ('C0000000-0000-0000-0000-000000000004', 1, 48),
  ('C0000000-0000-0000-0000-000000000004', 2, 16), ('C0000000-0000-0000-0000-000000000004', 3, 8),
  -- E-mail e colaboração
  ('C0000000-0000-0000-0000-000000000005', 0, 48), ('C0000000-0000-0000-0000-000000000005', 1, 24),
  ('C0000000-0000-0000-0000-000000000005', 2, 8),  ('C0000000-0000-0000-0000-000000000005', 3, 2),
  -- ERP e sistemas internos
  ('C0000000-0000-0000-0000-000000000006', 0, 72), ('C0000000-0000-0000-0000-000000000006', 1, 24),
  ('C0000000-0000-0000-0000-000000000006', 2, 8),  ('C0000000-0000-0000-0000-000000000006', 3, 4),
  -- Acesso a sistema legado (sem Crítica, ver cabeçalho)
  ('C0000000-0000-0000-0000-000000000007', 0, 120), ('C0000000-0000-0000-0000-000000000007', 1, 48),
  ('C0000000-0000-0000-0000-000000000007', 2, 24);

INSERT INTO MembrosDeEquipe (TecnicoId, EquipeId) VALUES
  ('A0000000-0000-0000-0000-000000000011', 'E0000000-0000-0000-0000-000000000001'),
  ('A0000000-0000-0000-0000-000000000012', 'E0000000-0000-0000-0000-000000000001'),
  ('A0000000-0000-0000-0000-000000000019', 'E0000000-0000-0000-0000-000000000001'),
  ('A0000000-0000-0000-0000-000000000021', 'E0000000-0000-0000-0000-000000000002'),
  ('A0000000-0000-0000-0000-000000000022', 'E0000000-0000-0000-0000-000000000002'),
  ('A0000000-0000-0000-0000-000000000031', 'E0000000-0000-0000-0000-000000000003');

COMMIT;
