import { en } from '@blocknote/core/locales';

const titles: Record<string, [string, string, string]> = {
  heading: ['Overskrift 1', 'Stor overskrift', 'Overskrifter'], heading_2: ['Overskrift 2', 'Overskrift til afsnit', 'Overskrifter'], heading_3: ['Overskrift 3', 'Mindre overskrift', 'Overskrifter'],
  heading_4: ['Overskrift 4', 'Underafsnit', 'Overskrifter'], heading_5: ['Overskrift 5', 'Underafsnit', 'Overskrifter'], heading_6: ['Overskrift 6', 'Underafsnit', 'Overskrifter'],
  toggle_heading: ['Foldbar overskrift 1', 'Fold afsnittet sammen', 'Overskrifter'], toggle_heading_2: ['Foldbar overskrift 2', 'Fold afsnittet sammen', 'Overskrifter'], toggle_heading_3: ['Foldbar overskrift 3', 'Fold afsnittet sammen', 'Overskrifter'],
  quote: ['Citat', 'Fremhæv et citat', 'Indhold'], toggle_list: ['Foldbar liste', 'Skjul eller vis underpunkter', 'Indhold'], numbered_list: ['Nummereret liste', 'Punkter i rækkefølge', 'Indhold'], bullet_list: ['Punktliste', 'Liste med punkter', 'Indhold'], check_list: ['Tjekliste', 'Liste med afkrydsning', 'Indhold'], paragraph: ['Tekst', 'Et almindeligt tekstafsnit', 'Indhold'],
  code_block: ['Kodeblok', 'Indsæt kildekode', 'Indhold'], page_break: ['Sideskift', 'Opdel dokumentet ved udskrift', 'Indhold'], divider: ['Skillelinje', 'Adskil afsnit', 'Indhold'],
  table: ['Tabel', 'En simpel tabel med redigerbare celler', 'Layout'], image: ['Billede', 'Indsæt et billede fra en URL', 'Medier'], video: ['Video', 'Indsæt en video fra en URL', 'Medier'], audio: ['Lyd', 'Indsæt lyd fra en URL', 'Medier'], file: ['Fil', 'Indsæt et link til en fil', 'Medier'], emoji: ['Emoji', 'Find og indsæt en emoji', 'Indhold'],
};
export const danish = {
  ...en,
  slash_menu: Object.fromEntries(Object.entries(en.slash_menu).map(([key,value]) => { const translation=titles[key]; return [key, translation ? {...value,title:translation[0],subtext:translation[1],group:translation[2],aliases:[...value.aliases,translation[0].toLowerCase()]} : value]; })) as typeof en.slash_menu,
  placeholders: {...en.placeholders, default: 'Skriv tekst, eller brug / til at tilføje indhold', heading: 'Overskrift', toggleListItem: 'Foldbart afsnit', bulletListItem:'Liste', numberedListItem:'Liste', checkListItem:'Liste'},
  side_menu: {add_block_label:'Tilføj blok',drag_handle_label:'Åbn blokmenu'},
  drag_handle:{delete_menuitem:'Slet',colors_menuitem:'Farver',header_row_menuitem:'Overskriftsrække',header_column_menuitem:'Overskriftskolonne'},
  table_handle:{delete_column_menuitem:'Slet kolonne',delete_row_menuitem:'Slet række',add_left_menuitem:'Tilføj kolonne til venstre',add_right_menuitem:'Tilføj kolonne til højre',add_above_menuitem:'Tilføj række ovenfor',add_below_menuitem:'Tilføj række nedenfor',split_cell_menuitem:'Opdel celle',merge_cells_menuitem:'Flet celler',background_color_menuitem:'Baggrundsfarve'},
  suggestion_menu:{no_items_title:'Ingen resultater'},
  toggle_blocks:{add_block_button:'Tomt afsnit. Tilføj en blok.',toggle_button:'Fold ud eller sammen'},
  formatting_toolbar:{...en.formatting_toolbar,bold:{...en.formatting_toolbar.bold,tooltip:'Fed'},italic:{...en.formatting_toolbar.italic,tooltip:'Kursiv'},underline:{...en.formatting_toolbar.underline,tooltip:'Understregning'},strike:{...en.formatting_toolbar.strike,tooltip:'Gennemstregning'},code:{...en.formatting_toolbar.code,tooltip:'Kode'},colors:{tooltip:'Farver'},link:{...en.formatting_toolbar.link,tooltip:'Indsæt link'},nest:{...en.formatting_toolbar.nest,tooltip:'Indryk blok'},unnest:{...en.formatting_toolbar.unnest,tooltip:'Ryk blok tilbage'}},
  color_picker:{text_title:'Tekst',background_title:'Baggrund',colors:{default:'Automatisk',gray:'Grå',brown:'Brun',red:'Rød',orange:'Orange',yellow:'Gul',green:'Grøn',blue:'Blå',purple:'Lilla',pink:'Pink'}},
  link_toolbar:{delete:{tooltip:'Fjern link'},edit:{text:'Rediger link',tooltip:'Rediger'},open:{tooltip:'Åbn i ny fane'},form:{title_placeholder:'Titel',url_placeholder:'URL'}},
  file_blocks:{add_button_text:{image:'Tilføj billede',video:'Tilføj video',audio:'Tilføj lyd',file:'Tilføj fil'}},
  file_panel:{upload:{title:'Upload',file_placeholder:{image:'Vælg billede',video:'Vælg video',audio:'Vælg lyd',file:'Vælg fil'},upload_error:'Filen kunne ikke uploades'},embed:{title:'Indsæt URL',embed_button:{image:'Indsæt billede',video:'Indsæt video',audio:'Indsæt lyd',file:'Indsæt fil'},url_placeholder:'Indsæt URL'}},
  generic:{...en.generic,loading:'Indlæser…',load_more:'Vis flere'},
};
