export type Customer = {
  id: string;
  company: string;
  initials: string;
  contact: string;
  role: string;
  location: string;
  focus: 'Beverages' | 'Condiments' | 'Confections';
  status: 'Active' | 'In Review';
  revenue: number;
  recent: boolean;
};

const accounts: Omit<Customer, 'id'>[] = [
  { company: 'Alfreds Futterkiste', initials: 'AF', contact: 'Maria Anders', role: 'Sales Representative', location: 'Berlin, Germany', focus: 'Beverages', status: 'Active', revenue: 184500, recent: true },
  { company: 'Ana Trujillo Emparedados', initials: 'AT', contact: 'Ana Trujillo', role: 'Owner / Executive', location: 'México D.F., Mexico', focus: 'Condiments', status: 'Active', revenue: 159240, recent: true },
  { company: 'Antonio Moreno Taquería', initials: 'AM', contact: 'Antonio Moreno', role: 'Order Specialist', location: 'México D.F., Mexico', focus: 'Beverages', status: 'In Review', revenue: 143200, recent: false },
  { company: 'Around the Horn', initials: 'AH', contact: 'Thomas Hardy', role: 'Sales Representative', location: 'London, UK', focus: 'Confections', status: 'Active', revenue: 127800, recent: true },
  { company: 'Berglunds snabbköp', initials: 'BS', contact: 'Christina Berglund', role: 'Order Administrator', location: 'Luleå, Sweden', focus: 'Beverages', status: 'Active', revenue: 115900, recent: false },
  { company: 'Blauer See Delikatessen', initials: 'BD', contact: 'Hanna Moos', role: 'Account Manager', location: 'Mannheim, Germany', focus: 'Condiments', status: 'Active', revenue: 97800, recent: true },
  { company: 'Bon app’', initials: 'BA', contact: 'Laurence Lebihan', role: 'Owner / Executive', location: 'Marseille, France', focus: 'Confections', status: 'In Review', revenue: 88200, recent: false },
  { company: 'Cactus Comidas', initials: 'CC', contact: 'Patricio Simpson', role: 'Sales Agent', location: 'Buenos Aires, Argentina', focus: 'Beverages', status: 'Active', revenue: 75400, recent: true },
  { company: 'Chop-suey Chinese', initials: 'CS', contact: 'Yang Wang', role: 'Owner / Executive', location: 'Bern, Switzerland', focus: 'Condiments', status: 'Active', revenue: 62600, recent: false },
  { company: 'Comércio Mineiro', initials: 'CM', contact: 'Pedro Afonso', role: 'Sales Associate', location: 'São Paulo, Brazil', focus: 'Beverages', status: 'Active', revenue: 55000, recent: true },
];

export const initialCustomers = accounts.map((customer, i) => ({ ...customer, id: `CUST-${String(102 + i).padStart(5, '0')}` }));

export type Order = {
  id: string;
  status: 'In Transit' | 'Delivered' | 'Scheduled';
  company: string;
  initials: string;
  rep: string;
  account: string;
  carrier: 'Federal Shipping' | 'Speedy Express' | 'United Package';
  ordered: string;
  required: string;
  shipped: string;
  freight: number;
  destination: string;
  address: string;
};

export const initialOrders: Order[] = [
  { id: '10248', status: 'In Transit', company: 'Vins et alcools Chevalier', initials: 'VC', rep: 'Buchanan, S.', account: 'C-0018', carrier: 'Federal Shipping', ordered: '1996-07-04', required: '1996-08-01', shipped: '1996-07-16', freight: 32.38, destination: 'Reims, France', address: '59 rue de l’Abbaye' },
  { id: '10249', status: 'Delivered', company: 'Toms Spezialitäten', initials: 'TS', rep: 'Suyama, M.', account: 'C-0082', carrier: 'Speedy Express', ordered: '1996-07-05', required: '1996-08-16', shipped: '1996-07-10', freight: 11.61, destination: 'Münster, Germany', address: 'Luisenstr. 48' },
  { id: '10250', status: 'Scheduled', company: 'Hanari Carnes', initials: 'HC', rep: 'Peacock, M.', account: 'C-0034', carrier: 'United Package', ordered: '1996-07-08', required: '1996-08-05', shipped: '1996-07-12', freight: 65.83, destination: 'Rio de Janeiro, Brazil', address: 'Rua do Paço, 67' },
  { id: '10251', status: 'In Transit', company: 'Victuailles en stock', initials: 'VS', rep: 'Leverling, J.', account: 'C-0089', carrier: 'Speedy Express', ordered: '1996-07-08', required: '1996-08-05', shipped: '1996-07-15', freight: 41.34, destination: 'Lyon, France', address: '2, rue du Commerce' },
  { id: '10252', status: 'Delivered', company: 'Suprêmes délices', initials: 'SD', rep: 'Peacock, M.', account: 'C-0077', carrier: 'United Package', ordered: '1996-07-09', required: '1996-08-06', shipped: '1996-07-11', freight: 51.30, destination: 'Charleroi, Belgium', address: 'Boulevard Tirou, 255' },
  ...initialCustomers.slice(0, 5).map((customer, i): Order => ({
    id: String(10253 + i), status: (['Scheduled', 'Delivered', 'In Transit'] as const)[i % 3],
    company: customer.company, initials: customer.initials, rep: customer.contact,
    account: customer.id, carrier: 'Federal Shipping', ordered: `1996-07-${10 + i}`,
    required: '1996-08-22', shipped: i % 3 === 0 ? '—' : '1996-07-19',
    freight: 16.25 + 8.19 * i, destination: customer.location, address: '',
  })),
];

export const money = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' });

export function exportCsv<T extends object>(filename: string, rows: T[], fields: (keyof T)[]) {
  const quote = (value: unknown) => {
    const text = String(value ?? '');
    const safe = typeof value === 'string' && /^[\s]*[=+\-@]/.test(text) ? `'${text}` : text;
    return `"${safe.replaceAll('"', '""')}"`;
  };
  const csv = [fields.map(quote), ...rows.map((row) => fields.map((field) => quote(row[field])))].map((row) => row.join(',')).join('\r\n');
  const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
